﻿using Avalonia;
using Avalonia.Media;
using Projektanker.Icons.Avalonia;
using Projektanker.Icons.Avalonia.MaterialDesign;
using System;

namespace FileTagger.NET
{
    internal class Program
    {
        // Initialization code. Don't use any Avalonia, third-party APIs or any
        // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
        // yet and stuff might break.
        [STAThread]
        public static void Main(string[] args) => BuildAvaloniaApp()
            .StartWithClassicDesktopLifetime(args);

        // Avalonia configuration, don't remove; also used by visual designer.
        public static AppBuilder BuildAvaloniaApp()
        {
            IconProvider.Current
                .Register<MaterialDesignIconProvider>();

            return AppBuilder.Configure<App>()
                        .UsePlatformDetect()
                        .With(new FontManagerOptions
                        {
                            DefaultFamilyName = "avares://FileTagger.NET/Assets/Fonts#MiSans"
                        })
                        .LogToTrace();
        }
    }
}