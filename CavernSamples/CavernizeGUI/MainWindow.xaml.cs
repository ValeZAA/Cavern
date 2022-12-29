﻿using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Shell;

using Cavern;
using Cavern.Format;
using Cavern.Format.Common;
using Cavern.Remapping;

using CavernizeGUI.Elements;
using CavernizeGUI.Resources;
using VoidX.WPF;

using Path = System.IO.Path;
using Track = CavernizeGUI.Elements.Track;

namespace CavernizeGUI {
    public partial class MainWindow : Window {
        /// <summary>
        /// Source of language strings.
        /// </summary>
        public static readonly ResourceDictionary language = new();

        /// <summary>
        /// Tells if a rendering process is in progress.
        /// </summary>
        public bool Rendering => taskEngine.IsOperationRunning;

        /// <summary>
        /// The matching displayed dot for each supported channel.
        /// </summary>
        readonly Dictionary<ReferenceChannel, Ellipse> channelDisplay;

        /// <summary>
        /// FFmpeg runner and locator.
        /// </summary>
        readonly FFmpeg ffmpeg;

        /// <summary>
        /// Playback environment used for rendering.
        /// </summary>
        readonly Listener listener;

        /// <summary>
        /// Queued conversions.
        /// </summary>
        readonly ObservableCollection<QueuedJob> jobs = new();

        /// <summary>
        /// Runs the process in the background.
        /// </summary>
        readonly TaskEngine taskEngine;

        AudioFile file;

        /// <summary>
        /// One-time UI transformations were applied.
        /// </summary>
        bool uiInitialized;

        /// <summary>
        /// Minimum window width that displays the queue. The window is resized to this width when a queue item is added.
        /// </summary>
        double minWidth;

        /// <summary>
        /// Initialize the window and load last settings.
        /// </summary>
        public MainWindow() {
            TaskbarItemInfo = new TaskbarItemInfo() {
                ProgressState = TaskbarItemProgressState.Normal
            };

            InitializeComponent();
            channelDisplay = new() {
                [ReferenceChannel.FrontLeft] = frontLeft,
                [ReferenceChannel.FrontCenter] = frontCenter,
                [ReferenceChannel.FrontRight] = frontRight,
                [ReferenceChannel.WideLeft] = wideLeft,
                [ReferenceChannel.WideRight] = wideRight,
                [ReferenceChannel.SideLeft] = sideLeft,
                [ReferenceChannel.SideRight] = sideRight,
                [ReferenceChannel.RearLeft] = rearLeft,
                [ReferenceChannel.RearRight] = rearRight,
                [ReferenceChannel.TopFrontLeft] = topFrontLeft,
                [ReferenceChannel.TopFrontCenter] = topFrontCenter,
                [ReferenceChannel.TopFrontRight] = topFrontRight,
                [ReferenceChannel.TopRearLeft] = topRearLeft,
                [ReferenceChannel.TopRearCenter] = topRearCenter,
                [ReferenceChannel.TopRearRight] = topRearRight
            };

            audio.ItemsSource = ExportFormat.Formats;
            audio.SelectedIndex = Settings.Default.outputCodec;

            ffmpeg = new(status, Settings.Default.ffmpegLocation);
            listener = new() { // Create a listener, which triggers the loading of saved environment settings
                UpdateRate = 64,
                AudioQuality = QualityModes.Perfect
            };

            language.Source = new Uri(";component/Resources/MainWindowStrings.xaml", UriKind.RelativeOrAbsolute);
            renderTarget.ItemsSource = RenderTarget.Targets;
            renderTarget.SelectedIndex = Math.Min(Math.Max(0, Settings.Default.renderTarget), RenderTarget.Targets.Length - 1);
            renderSettings.IsEnabled = true; // Don't grey out initially
            queuedJobs.ItemsSource = jobs;
            taskEngine = new(progress, TaskbarItemInfo, status);
            Reset();

            if (File.Exists(Settings.Default.hrirPath)) {
                hrir.IsChecked = TryLoadHRIR(false);
            }

            checkUpdates.IsChecked = Settings.Default.checkUpdates;
            if (Settings.Default.checkUpdates && !Program.ConsoleMode) {
                UpdateCheck.Perform(Settings.Default.lastUpdate, () => Settings.Default.lastUpdate = DateTime.Now);
            }
        }

        /// <summary>
        /// Displays an error message.
        /// </summary>
        static void Error(string error) =>
            MessageBox.Show(error, (string)language["Error"], MessageBoxButton.OK, MessageBoxImage.Error);

        /// <summary>
        /// Perform one-time UI updates after the window is initialized and displayed.
        /// </summary>
        protected override void OnActivated(EventArgs e) {
            if (!uiInitialized) {
                minWidth = Width;
                Width = queuedJobs.TransformToAncestor(this).Transform(new Point()).X;
                uiInitialized = true;
            }
        }

        /// <summary>
        /// Loads a content file into the application for processing.
        /// </summary>
        public void OpenContent(string path) {
            Reset();
            ffmpeg.CheckFFmpeg();
            taskEngine.UpdateProgressBar(0);
            OnOutputSelected(null, null);

#if RELEASE
            try {
#endif
                SetFile(new(path));
#if RELEASE
            } catch (IOException e) {
                Reset();
                throw new Exception(e.Message);
            } catch (Exception e) {
                Reset();
                throw new Exception($"{e.Message} {(string)language["Later"]}");
            }
#endif
        }

        /// <summary>
        /// Set up the window for an already loaded file.
        /// </summary>
        public void SetFile(AudioFile file) {
            fileName.Text = Path.GetFileName(file.Path);
            this.file = file;
            if (file.Tracks.Count != 0) {
                trackControls.Visibility = Visibility.Visible;
                tracks.ItemsSource = file.Tracks;
                tracks.SelectedIndex = 0;
                // Prioritize spatial codecs
                for (int i = 0, c = file.Tracks.Count; i < c; ++i) {
                    if (file.Tracks[i].Codec == Codec.EnhancedAC3) {
                        tracks.SelectedIndex = i;
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// Start rendering to a target file.
        /// </summary>
        public void RenderContent(string path) {
            Action renderTask;
            try {
                PreRender();
                renderTask = Render(path);
            } catch (Exception e) {
                Error(e.Message);
                return;
            }
            if (renderTask != null) {
                taskEngine.Run(renderTask);
            }
        }

        /// <summary>
        /// Save persistent settings on quit.
        /// </summary>
        protected override void OnClosed(EventArgs e) {
            Settings.Default.ffmpegLocation = ffmpeg.Location;
            Settings.Default.renderTarget = renderTarget.SelectedIndex;
            Settings.Default.outputCodec = audio.SelectedIndex;
            Settings.Default.checkUpdates = checkUpdates.IsChecked;
            Settings.Default.Save();
            base.OnClosed(e);
        }

        /// <summary>
        /// Reset the listener and remove the objects of the last render.
        /// </summary>
        void Reset() {
            listener.DetachAllSources();
            if (file != null && jobs.FirstOrDefault(x => x.IsUsingFile(file)) == null) {
                file.Dispose();
                file = null;
            }
            fileName.Text = string.Empty;
            trackControls.Visibility = Visibility.Hidden;
            tracks.ItemsSource = null;
            trackInfo.Text = string.Empty;
            report = (string)language["Reprt"];
        }

        /// <summary>
        /// Open file button event; loads an audio file to <see cref="reader"/>.
        /// </summary>
        void OpenFile(object _, RoutedEventArgs e) {
            if (taskEngine.IsOperationRunning) {
                Error((string)language["OpRun"]);
                return;
            }

            OpenFileDialog dialog = new() {
                Filter = string.Format((string)language["ImFmt"], AudioReader.filter)
            };
            if (dialog.ShowDialog().Value) {
#if RELEASE
                try {
#endif
                    OpenContent(dialog.FileName);
#if RELEASE
                } catch (Exception ex) {
                    Error(ex.Message);
                }
#endif
            }
        }

        /// <summary>
        /// Display the selected render target's active channels.
        /// </summary>
        void OnRenderTargetSelected(object _, SelectionChangedEventArgs e) {
            RenderTarget selected = (RenderTarget)renderTarget.SelectedItem;
            if (selected is DriverRenderTarget || selected is VirtualizerRenderTarget) {
                wiring.IsEnabled = false;
                foreach (KeyValuePair<ReferenceChannel, Ellipse> pair in channelDisplay) {
                    pair.Value.Fill = yellow;
                }
                return;
            }

            wiring.IsEnabled = true;
            foreach (KeyValuePair<ReferenceChannel, Ellipse> pair in channelDisplay) {
                pair.Value.Fill = red;
            }

            ReferenceChannel[] channels = selected.Channels;
            for (int ch = 0; ch < channels.Length; ++ch) {
                if (channelDisplay.ContainsKey(channels[ch])) {
                    channelDisplay[channels[ch]].Fill = green;
                }
            }
        }

        /// <summary>
        /// Display track metadata on track selection.
        /// </summary>
        void OnTrackSelected(object _, SelectionChangedEventArgs e) {
            if (tracks.SelectedItem != null) {
                trackInfo.Text = ((Track)tracks.SelectedItem).Details;
            }
        }

        /// <summary>
        /// Grey out renderer settings when it's not applicable.
        /// </summary>
        void OnOutputSelected(object _, SelectionChangedEventArgs e) {
            ExportFormat format = (ExportFormat)audio.SelectedItem;
            renderSettings.IsEnabled = !format.Codec.IsEnvironmental();
        }

        /// <summary>
        /// Prompt the user to select FFmpeg's installation folder.
        /// </summary>
        void LocateFFmpeg(object _, RoutedEventArgs e) {
            if (taskEngine.IsOperationRunning) {
                Error((string)language["OpRun"]);
                return;
            }

            ffmpeg.Locate();
        }

        /// <summary>
        /// Start the rendering process.
        /// </summary>
        void Render(object _, RoutedEventArgs e) {
            Action renderTask = GetRenderTask();
            if (renderTask != null) {
                taskEngine.Run(renderTask);
            }
        }

        /// <summary>
        /// Queue a rendering process.
        /// </summary>
        void Queue(object _, RoutedEventArgs e) {
            Action renderTask = GetRenderTask();
            if (renderTask != null) {
                if (Width < minWidth) {
                    Width = minWidth;
                }
                jobs.Add(new QueuedJob(file, (Track)tracks.SelectedItem, (RenderTarget)renderTarget.SelectedItem,
                    (ExportFormat)audio.SelectedItem, renderTask));
            }
        }

        /// <summary>
        /// Removes a queued job.
        /// </summary>
        void RemoveQueued(object _, RoutedEventArgs e) {
            if (taskEngine.IsOperationRunning) {
                Error((string)language["ReQOp"]);
                return;
            } else if (queuedJobs.SelectedItem == null) {
                Error((string)language["ReQSe"]);
            } else {
                jobs.RemoveAt(queuedJobs.SelectedIndex);
            }
        }

        /// <summary>
        /// Start processing the queue.
        /// </summary>
        void StartQueue(object _, RoutedEventArgs e) {
            QueuedJob[] jobs = this.jobs.ToArray();
            taskEngine.Run(() => QueueRunnerTask(jobs));
        }

        void QueueDragEnter(object _, DragEventArgs e) {
            var dropPossible = e.Data != null && ((DataObject)e.Data).ContainsFileDropList();
            if (dropPossible) {
                e.Effects = DragDropEffects.Copy;
            }
        }

        void QueueDragOver(object _, DragEventArgs e) {
            e.Handled = true;
        }

        void QueueDrop(object _, DragEventArgs e) {
            if (e.Data is DataObject obj && obj.ContainsFileDropList()) {
                AudioFile oldFile = file;
                List<string> invalids = new List<string>();
                StringCollection files = obj.GetFileDropList();
                for (int i = 0, c = files.Count; i < c; i++) {
                    try {
                        OpenContent(files[i]);
                    } catch {
                        invalids.Add(Path.GetFileName(files[i]));
                        continue;
                    }

                    var renderTask = GetRenderTask();
                    if (renderTask != null) {
                        jobs.Add(new QueuedJob(file, (Track)tracks.SelectedItem, (RenderTarget)renderTarget.SelectedItem,
                            (ExportFormat)audio.SelectedItem, renderTask));
                    } else {
                        invalids.Add(Path.GetFileName(files[i]));
                    }
                }
                file = oldFile;
                if (invalids.Count != 0) {
                    Error($"{(string)language["DropI"]}\n{string.Join('\n', invalids)}");
                }
            }
        }

        /// <summary>
        /// Opens the software's documentation.
        /// </summary>
        void Guide(object _, RoutedEventArgs e) => Process.Start(new ProcessStartInfo {
            FileName = "http://cavern.sbence.hu/cavern/doc.php?p=CavernizeGUI",
            UseShellExecute = true
        });

        /// <summary>
        /// Shows information about the used Cavern library and its version.
        /// </summary>
        void About(object _, RoutedEventArgs e) => MessageBox.Show(Listener.Info);

        /// <summary>
        /// Open Cavern's website.
        /// </summary>
        void Ad(object _, RoutedEventArgs e) => Process.Start(new ProcessStartInfo {
            FileName = "http://cavern.sbence.hu",
            UseShellExecute = true
        });

        /// <summary>
        /// This option allows FFmpeg to encode up to 255 channels in select codecs.
        /// </summary>
        const string massivelyMultichannel = " -mapping_family 255";

        /// <summary>
        /// Green color used for active speaker display.
        /// </summary>
        static readonly SolidColorBrush green = new(Colors.Green);

        /// <summary>
        /// Yellow color used for speaker display when a dynamic render target is selected.
        /// </summary>
        static readonly SolidColorBrush yellow = new(Colors.Yellow);

        /// <summary>
        /// Red color used for active speaker display.
        /// </summary>
        static readonly SolidColorBrush red = new(Colors.Red);
    }
}