using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Foxnet.Desktop.Services;
using Foxnet.Session;
using Microsoft.Win32;
using System.Windows.Media;

namespace Foxnet.Desktop.ViewModels;

public sealed partial class ProfileViewModel : ScreenViewModel
{
    private readonly LocalSession _session;

    [ObservableProperty] private string _nickname = "";
    [ObservableProperty] private string _nicknameDraft = "";
    [ObservableProperty] private ImageSource? _avatar;

    public ProfileViewModel(LocalSession session)
    {
        _session = session;
        var profile = session.GetProfile();
        Nickname = NicknameDraft = profile.Nickname;
        Run(() => Avatar = AvatarImages.Load(profile.Avatar));
    }

    [RelayCommand]
    private void SaveNickname() => Run(() =>
    {
        _session.Rename(NicknameDraft);
        Nickname = NicknameDraft = NicknameDraft.Trim();
        Notice = "Никнейм сохранен.";
    });

    [RelayCommand]
    private void PickAvatar()
    {
        var picker = new OpenFileDialog
        {
            Title = "Выберите аватар",
            Filter = "Изображения|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tif;*.tiff|Все файлы|*.*",
            CheckFileExists = true
        };
        if (picker.ShowDialog() != true)
            return;

        Run(() =>
        {
            var png = AvatarImages.Import(picker.FileName, LocalSession.AvatarSizeLimit);
            var image = AvatarImages.Load(png);
            _session.SetAvatar(png);
            Avatar = image;
            Notice = "Аватар обновлен.";
        });
    }

    [RelayCommand]
    private void ResetAvatar() => Run(() =>
    {
        _session.SetAvatar(null);
        Avatar = null;
        Notice = "Стандартный аватар восстановлен.";
    });
}
