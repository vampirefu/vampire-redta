using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using Microsoft.Win32;
using MapGenerator.Core;
using SixLabors.ImageSharp.Formats.Png;

namespace MapGenerator.Example
{
    public partial class MainWindow : Window
    {
        private string toolPath = string.Empty;
        private string gameResourcePath = string.Empty;
        private Storyboard? progressGlowStoryboard;
        private bool isProgressAnimating;

        public MainWindow()
        {
            InitializeComponent();
            LoadDefaultPaths();
            InitializeProgressAnimation();
        }

        private void InitializeProgressAnimation()
        {
            progressGlowStoryboard = (Storyboard)FindResource("ProgressGlowStoryboard");
        }

        private void StartProgressAnimation()
        {
            if (!isProgressAnimating && progressGlowStoryboard != null)
            {
                isProgressAnimating = true;
                progressGlowStoryboard.Begin(ProgressBar);
            }
        }

        private void StopProgressAnimation()
        {
            if (isProgressAnimating && progressGlowStoryboard != null)
            {
                isProgressAnimating = false;
                progressGlowStoryboard.Stop(ProgressBar);
                if (ProgressBar.Effect is DropShadowEffect glow)
                {
                    glow.Opacity = 0;
                }
            }
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void Window_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.LeftButton == System.Windows.Input.MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        private void Window_MouseMove(object sender, MouseEventArgs e)
        {
            var p = e.GetPosition(this);
            double cx = ActualWidth * 0.5;
            double cy = ActualHeight * 0.5;
            if (cx <= 0 || cy <= 0)
                return;

            double nx = (p.X - cx) / cx; // -1..1
            double ny = (p.Y - cy) / cy;

            double maxBg = 10.0;
            double maxParticles = 6.0;

            try
            {
                if (BgPanTransform != null)
                {
                    BgPanTransform.X = nx * maxBg;
                    BgPanTransform.Y = ny * maxBg * 0.6;
                }

                if (Particle1Transform != null)
                {
                    Particle1Transform.X = nx * maxParticles * -1.0;
                    Particle1Transform.Y = ny * maxParticles * -0.6;
                }
                if (Particle2Transform != null)
                {
                    Particle2Transform.X = nx * maxParticles * 0.9;
                    Particle2Transform.Y = ny * maxParticles * 0.4;
                }
                if (Particle3Transform != null)
                {
                    Particle3Transform.X = nx * maxParticles * -0.6;
                    Particle3Transform.Y = ny * maxParticles * 0.6;
                }
                if (Particle4Transform != null)
                {
                    Particle4Transform.X = nx * maxParticles * 0.8;
                    Particle4Transform.Y = ny * maxParticles * -0.3;
                }

                if (ScanlineTransform != null)
                    ScanlineTransform.Y = ny * 6;
                if (Scanline2Transform != null)
                    Scanline2Transform.Y = ny * 8;
                if (Halo1Transform != null)
                {
                    Halo1Transform.X = nx * 18;
                    Halo1Transform.Y = ny * 8;
                }
            }
            catch
            {
                // 保守处理：若运行时找不到元素或线程竞态，则忽略
            }
        }

        private void LoadDefaultPaths()
        {
            string defaultToolPath = @"C:\Users\Administrator\Desktop\AGWarDTA\Resources\RandomMapGenerator_RA2";
            if (Directory.Exists(defaultToolPath))
            {
                toolPath = defaultToolPath;
                ToolPathTextBox.Text = toolPath;
            }

            string defaultGameResourcePath = @"C:\Users\Administrator\Desktop\AGWarDTA";
            if (Directory.Exists(defaultGameResourcePath))
            {
                gameResourcePath = defaultGameResourcePath;
                GameResourcePathTextBox.Text = gameResourcePath;
            }
        }

        private void BrowseToolPathButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFolderDialog dialog = new OpenFolderDialog
            {
                Title = "选择工具路径 (RandomMapGenerator_RA2)"
            };

            if (dialog.ShowDialog() == true)
            {
                toolPath = dialog.FolderName;
                ToolPathTextBox.Text = toolPath;
                LogMessage($"工具路径已设置: {toolPath}");
            }
        }

        private void BrowseGameResourcePathButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFolderDialog dialog = new OpenFolderDialog
            {
                Title = "选择游戏资源路径"
            };

            if (dialog.ShowDialog() == true)
            {
                gameResourcePath = dialog.FolderName;
                GameResourcePathTextBox.Text = gameResourcePath;
                LogMessage($"游戏资源路径已设置: {gameResourcePath}");
            }
        }

        private bool ValidatePaths()
        {
            if (string.IsNullOrWhiteSpace(toolPath) || !Directory.Exists(toolPath))
            {
                MessageBox.Show("请先设置有效的工具路径", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            if (string.IsNullOrWhiteSpace(gameResourcePath) || !Directory.Exists(gameResourcePath))
            {
                MessageBox.Show("请先设置有效的游戏资源路径", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            return true;
        }

        private string GetSelectedClimate()
        {
            return ClimateComboBox.SelectedIndex switch
            {
                0 => "TEMPERATE",
                1 => "TEMPERATE_Islands",
                2 => "NEWURBAN",
                3 => "DESERT",
                _ => "TEMPERATE"
            };
        }

        private int GetSelectedPlayerCount()
        {
            return PlayerCountComboBox.SelectedIndex + 2;
        }

        private int GetSelectedMapSize()
        {
            return MapSizeComboBox.SelectedIndex;
        }

        private void GenerateMapButton_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidatePaths())
                return;

            SetControlsEnabled(false);
            UpdateProgress(0, "正在初始化...");

            RandomMapOptions options = new RandomMapOptions
            {
                Climate = GetSelectedClimate(),
                PlayerCount = GetSelectedPlayerCount(),
                Size = GetSelectedMapSize(),
                RandomBuildingDamage = RandomDamageCheckBox.IsChecked ?? true
            };

            LogMessage("正在生成随机地图...");

            Task.Run(() =>
            {
                try
                {
                    RandomMapGenerator generator = new RandomMapGenerator(toolPath, gameResourcePath);
                    generator.ProgressChanged += (s, args) =>
                    {
                        UpdateProgress(args.Progress, args.Message);
                        LogMessage(args.Message);
                    };
                    generator.GenerateRandomMap(options);

                    string outputPath = generator.OutputPath;
                    string mapPath = Path.Combine(outputPath, "Maps", "Custom", "随机地图.map");
                    string previewPath = Path.Combine(outputPath, "Maps", "Custom", "随机地图.png");

                    LogMessage($"随机地图生成完成!");
                    LogMessage($"地图文件: {mapPath}");
                    LogMessage($"缩略图: {previewPath}");

                    Dispatcher.BeginInvoke(() =>
                    {
                        MessageBox.Show("随机地图生成完成!", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                    });
                }
                catch (Exception ex)
                {
                    LogMessage($"生成随机地图失败: {ex.Message}");
                    Dispatcher.BeginInvoke(() =>
                    {
                        MessageBox.Show($"生成随机地图失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    });
                }
                finally
                {
                    SetControlsEnabled(true);
                }
            });
        }

        private void RenderThumbnailButton_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidatePaths())
                return;

            OpenFileDialog dialog = new OpenFileDialog
            {
                Title = "选择地图文件",
                Filter = "地图文件 (*.map)|*.map",
                InitialDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Maps", "Custom")
            };

            if (dialog.ShowDialog() != true)
                return;

            string mapPath = dialog.FileName;
            string outputName = Path.GetFileNameWithoutExtension(mapPath);

            SetControlsEnabled(false);
            UpdateProgress(0, "正在初始化...");

            LogMessage($"正在渲染缩略图: {mapPath}");

            Task.Run(() =>
            {
                try
                {
                    string rendererPath = Path.Combine(toolPath, "Map Renderer");
                    MapThumbnailRenderer renderer = new MapThumbnailRenderer(rendererPath, gameResourcePath);
                    renderer.ProgressChanged += (s, args) =>
                    {
                        UpdateProgress(args.Progress, args.Message);
                        LogMessage(args.Message);
                    };
                    renderer.SetOutputPath(Path.GetDirectoryName(mapPath) ?? AppDomain.CurrentDomain.BaseDirectory);
                    renderer.RenderThumbnail(mapPath, outputName);

                    string thumbnailPath = Path.Combine(renderer.OutputPath, $"{outputName}.png");

                    LogMessage($"缩略图渲染完成!");
                    LogMessage($"输出文件: {thumbnailPath}");

                    Dispatcher.BeginInvoke(() =>
                    {
                        MessageBox.Show("缩略图渲染完成!", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                    });
                }
                catch (Exception ex)
                {
                    LogMessage($"渲染缩略图失败: {ex.Message}");
                    Dispatcher.BeginInvoke(() =>
                    {
                        MessageBox.Show($"渲染缩略图失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    });
                }
                finally
                {
                    SetControlsEnabled(true);
                }
            });
        }

        private void ExtractPreviewButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog
            {
                Title = "选择地图文件",
                Filter = "地图文件 (*.map)|*.map",
                InitialDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Maps", "Custom")
            };

            if (dialog.ShowDialog() != true)
                return;

            string mapPath = dialog.FileName;

            SetControlsEnabled(false);
            UpdateProgress(0, "正在提取缩略图...");

            LogMessage($"正在提取缩略图: {mapPath}");

            Task.Run(() =>
            {
                try
                {
                    var image = MapPreviewExtractor.ExtractMapPreview(mapPath);
                    if (image != null)
                    {
                        UpdateProgress(50, "正在保存图片...");

                        string outputName = Path.GetFileNameWithoutExtension(mapPath);
                        string outputPath = Path.Combine(Path.GetDirectoryName(mapPath) ?? AppDomain.CurrentDomain.BaseDirectory, $"{outputName}_extracted.png");
                        using (FileStream stream = new FileStream(outputPath, FileMode.Create))
                        {
                            image.Save(stream, new PngEncoder());
                        }

                        UpdateProgress(100, "完成!");
                        LogMessage("缩略图提取完成!");
                        LogMessage($"输出文件: {outputPath}");

                        Dispatcher.BeginInvoke(() =>
                        {
                            MessageBox.Show("缩略图提取完成!", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                        });
                    }
                    else
                    {
                        LogMessage("无法提取缩略图，地图文件可能没有预览数据");
                        Dispatcher.BeginInvoke(() =>
                        {
                            MessageBox.Show("无法提取缩略图，地图文件可能没有预览数据", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                        });
                    }
                }
                catch (Exception ex)
                {
                    LogMessage($"提取缩略图失败: {ex.Message}");
                    Dispatcher.BeginInvoke(() =>
                    {
                        MessageBox.Show($"提取缩略图失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    });
                }
                finally
                {
                    SetControlsEnabled(true);
                }
            });
        }

        private void UpdateProgress(int progress, string message)
        {
            Dispatcher.BeginInvoke(() =>
            {
                ProgressBar.Value = progress;
                ProgressTextBlock.Text = $"{message} ({progress}%)";

                if (progress > 0 && progress < 100)
                {
                    StartProgressAnimation();
                }
                else
                {
                    StopProgressAnimation();
                }
            });
        }

        private void SetControlsEnabled(bool enabled)
        {
            Dispatcher.BeginInvoke(() =>
            {
                GenerateMapButton.IsEnabled = enabled;
                RenderThumbnailButton.IsEnabled = enabled;
                ExtractPreviewButton.IsEnabled = enabled;
                BrowseToolPathButton.IsEnabled = enabled;
                BrowseGameResourcePathButton.IsEnabled = enabled;

                if (enabled)
                {
                    StopProgressAnimation();
                    UpdateProgress(0, "就绪");
                }
            });
        }

        private void LogMessage(string message)
        {
            Dispatcher.BeginInvoke(() =>
            {
                LogTextBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}\n");
                LogTextBox.ScrollToEnd();
            });
        }
    }
}
