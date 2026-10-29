using System;
using System.Collections.Generic;
using DeadCounty.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace DeadCounty.Player
{
    // The Audio page of the options: the five volumes of GameSettings as bars, like the mouse sensitivity.
    public partial class GameMenu
    {
        const float VolumeStep = 0.1f;

        sealed class VolumeRow
        {
            public VisualElement Fill;
            public Func<float> Get;
            public Action<float> Set;
        }

        readonly List<VolumeRow> volumeRows = new();

        List<Item> BuildAudio(VisualElement root) => new()
        {
            VolumeEntry(root, "option-volume-master", () => GameSettings.MasterVolume, v => GameSettings.MasterVolume = v),
            VolumeEntry(root, "option-volume-effects", () => GameSettings.EffectsVolume, v => GameSettings.EffectsVolume = v),
            VolumeEntry(root, "option-volume-ambience", () => GameSettings.AmbienceVolume, v => GameSettings.AmbienceVolume = v),
            VolumeEntry(root, "option-volume-music", () => GameSettings.MusicVolume, v => GameSettings.MusicVolume = v),
            VolumeEntry(root, "option-volume-interface", () => GameSettings.InterfaceVolume, v => GameSettings.InterfaceVolume = v),
            Entry(root, "audio-back", () => ShowPage(settingsItems), sound: MenuFeedback.Back),
        };

        // Left and right step the volume by tenths; a click on the bar sets it there.
        Item VolumeEntry(VisualElement root, string elementName, Func<float> get, Action<float> set)
        {
            var row = new VolumeRow { Fill = Find<VisualElement>(root, elementName + "-fill"), Get = get, Set = set };
            volumeRows.Add(row);
            var bar = row.Fill.parent;
            bar.pickingMode = PickingMode.Position;
            bar.RegisterCallback<ClickEvent>(e =>
            {
                SetVolume(row, e.localPosition.x / bar.resolvedStyle.width);
                Feedback?.Invoke(MenuFeedback.Step);
            });
            // Rounded so repeated steps land on whole tenths.
            return Entry(root, elementName, null, step => SetVolume(row, Mathf.Round((get() + step * VolumeStep) * 10f) / 10f));
        }

        void SetVolume(VolumeRow row, float value)
        {
            row.Set(Mathf.Clamp01(value));
            ShowValues();
        }

        void ShowVolumes()
        {
            foreach (var row in volumeRows) row.Fill.style.width = Length.Percent(row.Get() * 100f);
        }
    }
}
