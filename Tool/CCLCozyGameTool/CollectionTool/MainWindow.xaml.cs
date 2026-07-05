using Assets.Scripts.Contents.CollectionSystem.Model;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;

namespace CollectionTool
{
    public partial class MainWindow : Window
    {
        private ObservableCollection<CollectionData> _entries = new();
        private CollectionData? _selected;

        // 불러온 시점(또는 마지막 알림 시점)에 이미 존재했던 항목들. 여기 없는 Id가 처음 Apply되면 "신규 추가"로 간주해 디스코드로 알린다.
        private readonly HashSet<Guid> _notifiedIds = new();

        public MainWindow()
        {
            InitializeComponent();
            EntryListBox.ItemsSource = _entries;

            Loaded += (_, _) =>
            {
                if (File.Exists(RepoPaths.ExportedGameDataFilePath))
                    LoadFromDatFile(showConfirm: false);
            };
        }

        private void ImportFromDatButton_Click(object sender, RoutedEventArgs e) =>
            LoadFromDatFile(showConfirm: true);

        private void LoadFromDatFile(bool showConfirm)
        {
            if (!File.Exists(RepoPaths.ExportedGameDataFilePath))
            {
                SetStatus("collection.dat 파일이 없습니다: " + RepoPaths.ExportedGameDataFilePath);
                return;
            }

            if (showConfirm)
            {
                var result = MessageBox.Show(
                    "collection.dat에서 다시 불러옵니다.\n현재 편집 중인 내용은 사라집니다. 계속하시겠습니까?",
                    "다시 불러오기",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);
                if (result != MessageBoxResult.Yes) return;
            }

            try
            {
                var data = CollectionDataStorage.LoadEncrypted(RepoPaths.ExportedGameDataFilePath);
                _entries = new ObservableCollection<CollectionData>(data.collectionDataList);
                EntryListBox.ItemsSource = _entries;
                _selected = null;
                DetailPanel.IsEnabled = false;
                EditOverlay.Visibility = Visibility.Visible;
                UpdateSaveButtonState();

                _notifiedIds.Clear();
                foreach (var entry in _entries)
                    _notifiedIds.Add(entry.CollectionId);

                SetStatus($"불러오기 완료 ({_entries.Count}개) - {RepoPaths.ExportedGameDataFilePath}");
            }
            catch (Exception ex)
            {
                SetStatus("불러오기 실패: " + ex.Message);
            }
        }

        private void ExportButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var data = new CollectionBoolData { collectionDataList = _entries.ToList() };
                CollectionDataStorage.SaveEncrypted(RepoPaths.ExportedGameDataFilePath, data);
                SetStatus("게임용 내보내기 완료(암호화) - " + RepoPaths.ExportedGameDataFilePath);
            }
            catch (Exception ex)
            {
                SetStatus("내보내기 실패: " + ex.Message);
            }
        }

        private void AddButton_Click(object sender, RoutedEventArgs e)
        {
            var item = new CollectionData { Name = "새 항목" };
            _entries.Add(item);
            EntryListBox.SelectedItem = item;
        }

        private void RemoveButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selected == null) return;
            _entries.Remove(_selected);
            _selected = null;
            DetailPanel.IsEnabled = false;
            EditOverlay.Visibility = Visibility.Visible;
            UpdateSaveButtonState();
        }

        private void EntryListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _selected = EntryListBox.SelectedItem as CollectionData;
            if (_selected == null)
            {
                DetailPanel.IsEnabled = false;
                EditOverlay.Visibility = Visibility.Visible;
                UpdateSaveButtonState();
                return;
            }

            DetailPanel.IsEnabled = true;
            EditOverlay.Visibility = Visibility.Collapsed;

            NameTextBox.Text = _selected.Name;
            HobbyTextBox.Text = _selected.Hobby;
            AgeTextBox.Text = _selected.Age == 0 ? string.Empty : _selected.Age.ToString();
            HeightTextBox.Text = _selected.Height == 0 ? string.Empty : _selected.Height.ToString();
            BirthdayDatePicker.SelectedDate = _selected.Birthday == DateTime.MinValue ? null : _selected.Birthday;

            SetPreview(MainHiddenImage, _selected.ProfilePictureBase64_Main_Hidden);
            SetPreview(SubHiddenImage, _selected.ProfilePictureBase64_Sub_Hidden);
            SetPreview(MainImage, _selected.ProfilePictureBase64_Main);
            SetPreview(SubImage, _selected.ProfilePictureBase64_Sub);

            UpdateSaveButtonState();
        }

        private void RequiredField_Changed(object sender, TextChangedEventArgs e) => UpdateSaveButtonState();

        private void RequiredField_DateChanged(object sender, SelectionChangedEventArgs e) => UpdateSaveButtonState();

        private bool IsFormValid()
        {
            if (_selected == null) return false;
            if (string.IsNullOrWhiteSpace(NameTextBox.Text)) return false;
            if (string.IsNullOrWhiteSpace(HobbyTextBox.Text)) return false;
            if (!int.TryParse(AgeTextBox.Text, out _)) return false;
            if (!int.TryParse(HeightTextBox.Text, out _)) return false;
            if (BirthdayDatePicker.SelectedDate == null) return false;
            return true;
        }

        private void UpdateSaveButtonState()
        {
            var valid = IsFormValid();
            ApplyButton.IsEnabled = valid;

            if (valid)
            {
                StartSaveButtonBlink();
            }
            else
            {
                StopSaveButtonBlink();
            }
        }

        private void StartSaveButtonBlink()
        {
            var animation = new DoubleAnimation
            {
                From = 1.0,
                To = 0.45,
                Duration = TimeSpan.FromSeconds(0.6),
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever
            };
            ApplyButton.BeginAnimation(OpacityProperty, animation);
        }

        private void StopSaveButtonBlink()
        {
            ApplyButton.BeginAnimation(OpacityProperty, null);
            ApplyButton.Opacity = 1.0;
        }

        private void ApplyButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selected == null || !IsFormValid()) return;

            StopSaveButtonBlink();

            _selected.Name = NameTextBox.Text;
            _selected.Hobby = HobbyTextBox.Text;
            _selected.Age = int.TryParse(AgeTextBox.Text, out var age) ? age : 0;
            _selected.Height = int.TryParse(HeightTextBox.Text, out var height) ? height : 0;
            _selected.Birthday = BirthdayDatePicker.SelectedDate ?? DateTime.MinValue;

            EntryListBox.Items.Refresh();

            if (_notifiedIds.Add(_selected.CollectionId))
            {
                // 이번에 처음 적용된(=새로 추가된) 항목일 때만 알린다. 같은 항목을 다시 고쳐도 재알림하지 않는다.
                _ = NotifyNewEntryAsync(_selected);
            }

            SetStatus("선택 항목에 적용됨 (파일 저장은 별도로 눌러야 반영됩니다)");
        }

        private async System.Threading.Tasks.Task NotifyNewEntryAsync(CollectionData data)
        {
            var message = new StringBuilder()
                .AppendLine($"📖 새 도감 항목이 추가되었습니다.(등록자:{Dns.GetHostName()})")
                .AppendLine($"이름: {data.Name}")
                .AppendLine($"나이: {data.Age}")
                .AppendLine($"키: {data.Height}cm")
                .AppendLine($"생일: {(data.Birthday == DateTime.MinValue ? "-" : data.Birthday.ToString("yyyy-MM-dd"))}")
                .AppendLine($"취미: {data.Hobby}")
                .Append($"CollectionId: {data.CollectionId}")
                .ToString();

            var sent = await DiscordNotifier.TrySendAsync(message);
            SetStatus(sent ? "디스코드 알림 전송 완료" : "디스코드 알림 전송 실패 (웹훅 설정을 확인하세요)");
        }

        private void UploadMainHidden_Click(object sender, RoutedEventArgs e) => UploadInto(p => _selected!.ProfilePictureBase64_Main_Hidden = p, MainHiddenImage);
        private void UploadSubHidden_Click(object sender, RoutedEventArgs e) => UploadInto(p => _selected!.ProfilePictureBase64_Sub_Hidden = p, SubHiddenImage);
        private void UploadMain_Click(object sender, RoutedEventArgs e) => UploadInto(p => _selected!.ProfilePictureBase64_Main = p, MainImage);
        private void UploadSub_Click(object sender, RoutedEventArgs e) => UploadInto(p => _selected!.ProfilePictureBase64_Sub = p, SubImage);

        private void ClearMainHidden_Click(object sender, RoutedEventArgs e) => ClearPhoto(() => _selected!.ProfilePictureBase64_Main_Hidden = null!, MainHiddenImage);
        private void ClearSubHidden_Click(object sender, RoutedEventArgs e) => ClearPhoto(() => _selected!.ProfilePictureBase64_Sub_Hidden = null!, SubHiddenImage);
        private void ClearMain_Click(object sender, RoutedEventArgs e) => ClearPhoto(() => _selected!.ProfilePictureBase64_Main = null!, MainImage);
        private void ClearSub_Click(object sender, RoutedEventArgs e) => ClearPhoto(() => _selected!.ProfilePictureBase64_Sub = null!, SubImage);

        private void UploadInto(Action<PhotoData> setter, Image previewImage)
        {
            if (_selected == null) return;

            var dialog = new OpenFileDialog
            {
                Filter = "이미지 파일|*.png;*.jpg;*.jpeg;*.bmp"
            };
            if (dialog.ShowDialog() != true) return;

            var bytes = File.ReadAllBytes(dialog.FileName);
            var photo = new PhotoData { PhotoBase64 = Convert.ToBase64String(bytes) };
            setter(photo);
            SetPreview(previewImage, photo);
        }

        private void ClearPhoto(Action clearSetter, Image previewImage)
        {
            if (_selected == null) return;
            clearSetter();
            SetPreview(previewImage, null);
        }

        private static void SetPreview(Image image, PhotoData? photo)
        {
            if (photo == null || string.IsNullOrEmpty(photo.PhotoBase64))
            {
                image.Source = null;
                return;
            }

            var bytes = Convert.FromBase64String(photo.PhotoBase64);
            using var ms = new MemoryStream(bytes);
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.StreamSource = ms;
            bitmap.EndInit();
            bitmap.Freeze();
            image.Source = bitmap;
        }

        private void SetStatus(string message) => StatusText.Text = message;
    }
}
