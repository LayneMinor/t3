﻿using System;
using System.Linq;
using System.Numerics;
using ImGuiNET;
using T3.Core.Animation;
using T3.Core.Operator;
using T3.Core.Utils;
using T3.Editor.Gui.Styling;
using T3.Editor.Gui.UiHelpers;
using T3.Operators.Types.Id_59a0458e_2f3a_4856_96cd_32936f783cc5;

namespace T3.Editor.Gui.ChildUi
{
    public static class MidiInputUi
    {
        public static SymbolChildUi.CustomUiResult DrawChildUi(Instance instance, ImDrawListPtr drawList, ImRect screenRect)
        {
            if (!(instance is MidiInput midiInput)
                || !ImGui.IsRectVisible(screenRect.Min, screenRect.Max))
                return SymbolChildUi.CustomUiResult.None;
            
            
            
            
            // Draw label and current value
            ImGui.SetCursorScreenPos(screenRect.Min);
            ImGui.BeginGroup();
            ImGui.PushClipRect(screenRect.Min, screenRect.Max, true);

            const float flashDuration = 0.6f;
            // Flash on changes
            var flashProgress = (float)(Playback.RunTimeInSecs - midiInput.LastMessageTime).Clamp(0,flashDuration)/flashDuration;
            if (flashProgress < 1)
            {
                drawList.AddRectFilled(screenRect.Min, screenRect.Max, 
                                       Color.Mix(UiColors.StatusAnimated.Fade(0.8f), 
                                                 Color.Transparent, 
                                                 MathF.Pow(flashProgress*flashProgress, 0.5f)));
            }
            

            ImGui.PushFont(Fonts.FontSmall);

            var deviceAndChannel = "Midi Device?";
            if (!string.IsNullOrEmpty(midiInput.Device.TypedInputValue.Value))
            {
                deviceAndChannel = $"{midiInput.Device.TypedInputValue.Value} CH{midiInput.Channel.TypedInputValue.Value}:{midiInput.Control.TypedInputValue.Value}";
            }

            ImGui.TextUnformatted(deviceAndChannel);

            var renamedTitle = midiInput.Parent.Symbol.Children.Single(c => c.Id == midiInput.SymbolChildId).Name;
            if (!string.IsNullOrEmpty(renamedTitle))
            {
                ImGui.TextUnformatted($"\"{renamedTitle}\"");
            }

            var normalizedFadeOut = ((Playback.RunTimeInSecs - midiInput.LastMessageTime) / 5).Clamp(0, 1);
            var fadeOut = (float)MathUtils.RemapAndClamp(normalizedFadeOut, 0, 1, 1, 0.5f);
            var fadeColor = UiColors.ForegroundFull.Fade(fadeOut);
            ImGui.TextColored(fadeColor, $"{midiInput.Result.Value:0.00}");

            ImGui.PopClipRect();
            ImGui.EndGroup();

            // Drag mini graph
            var graphRect = screenRect;
            const int padding = -3;

            graphRect.Expand(padding);
            if (graphRect.GetHeight() > 0 && graphRect.GetWidth() > 0)
            {
                var minRange = midiInput.OutputRange.TypedInputValue.Value.X;
                var maxRange = midiInput.OutputRange.TypedInputValue.Value.Y;
                var currentValue = midiInput.Result.Value;

                var xPos = MathUtils.RemapAndClamp((double)currentValue, minRange, maxRange, graphRect.Min.X, graphRect.Max.X);
                var topLeftPos = new Vector2((float)xPos, graphRect.Min.Y);
                drawList.AddRectFilled(topLeftPos, topLeftPos + new Vector2(1, graphRect.GetHeight()), UiColors.StatusAnimated);
            }

            ImGui.PopFont();

            return SymbolChildUi.CustomUiResult.Rendered | SymbolChildUi.CustomUiResult.PreventInputLabels;
        }
    }
}
