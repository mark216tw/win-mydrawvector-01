using Microsoft.UI.Xaml.Media.Imaging;
using MyDrawVector.Core.Models;

namespace MyDrawVector.Services;

public sealed record StoredImage(AssetInfo Info, byte[] Data, BitmapImage Bitmap);
