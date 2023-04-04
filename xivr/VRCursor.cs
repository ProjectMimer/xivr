using Dalamud;
using Dalamud.Logging;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.System.Memory;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using static FFXIVClientStructs.FFXIV.Client.UI.AddonNamePlate;

namespace xivr
{
    
    public static unsafe class VRCursor
    {
        private static NamePlateObject* currentNPTarget = null;

        

        public static unsafe bool SetupVRTargetCursor(AtkTextNode** vrTrgCursor)
        {
            if ((*vrTrgCursor) != null)
            {
                return true;
            }

            (*vrTrgCursor) = (AtkTextNode*)IMemorySpace.GetUISpace()->Malloc((ulong)sizeof(AtkTextNode), 8);
            if ((*vrTrgCursor) == null)
            {
                PluginLog.Debug("Failed to allocate memory for text node");
                return false;
            }
            IMemorySpace.Memset((*vrTrgCursor), 0, (ulong)sizeof(AtkTextNode));
            (*vrTrgCursor)->Ctor();

            (*vrTrgCursor)->AtkResNode.Type = NodeType.Text;
            (*vrTrgCursor)->AtkResNode.Flags = (short)(NodeFlags.UseDepthBasedPriority);
            (*vrTrgCursor)->AtkResNode.DrawFlags = 12;

            (*vrTrgCursor)->LineSpacing = 12;
            (*vrTrgCursor)->AlignmentFontType = 4;
            (*vrTrgCursor)->FontSize = (byte)xivr.cfg.data.targetCursorSize;
            (*vrTrgCursor)->TextFlags = (byte)(TextFlags.AutoAdjustNodeSize | TextFlags.Edge);
            (*vrTrgCursor)->TextFlags2 = 0;

            (*vrTrgCursor)->SetText("↓");

            (*vrTrgCursor)->AtkResNode.ToggleVisibility(true);

            (*vrTrgCursor)->AtkResNode.SetPositionShort(90, -23);
            ushort outWidth = 0;
            ushort outHeight = 0;
            (*vrTrgCursor)->GetTextDrawSize(&outWidth, &outHeight);
            (*vrTrgCursor)->AtkResNode.SetWidth((ushort)(outWidth));
            (*vrTrgCursor)->AtkResNode.SetHeight((ushort)(outHeight));

            // white fill
            (*vrTrgCursor)->TextColor.R = 255;
            (*vrTrgCursor)->TextColor.G = 255;
            (*vrTrgCursor)->TextColor.B = 255;
            (*vrTrgCursor)->TextColor.A = 255;

            // yellow/golden glow
            (*vrTrgCursor)->EdgeColor.R = 235;
            (*vrTrgCursor)->EdgeColor.G = 185;
            (*vrTrgCursor)->EdgeColor.B = 7;
            (*vrTrgCursor)->EdgeColor.A = 255;

            return true;
        }

        public static void FreeVRTargetCursor(AtkTextNode** vrTrgCursor)
        {
            if ((*vrTrgCursor) != null)
            {
                if (currentNPTarget != null)
                    RemoveVRCursor(vrTrgCursor, currentNPTarget);

                currentNPTarget = null;

                (*vrTrgCursor)->AtkResNode.Destroy(true);
                (*vrTrgCursor) = null;
            }
        }

        public static void AddVRCursor(AtkTextNode** vrTrgCursor, NamePlateObject* nameplate)
        {
            if (nameplate != null && (*vrTrgCursor) != null)
            {
                var npComponent = nameplate->RootNode->Component;

                var lastChild = npComponent->UldManager.RootNode;
                while (lastChild->PrevSiblingNode != null) lastChild = lastChild->PrevSiblingNode;

                lastChild->PrevSiblingNode = (AtkResNode*)(*vrTrgCursor);
                (*vrTrgCursor)->AtkResNode.NextSiblingNode = lastChild;
                (*vrTrgCursor)->AtkResNode.ParentNode = (AtkResNode*)nameplate->RootNode;

                npComponent->UldManager.UpdateDrawNodeList();
            }
        }

        public static void RemoveVRCursor(AtkTextNode** vrTrgCursor, NamePlateObject* nameplate)
        {
            if (nameplate != null && (*vrTrgCursor) != null)
            {
                var npComponent = nameplate->RootNode->Component;

                var lastChild = npComponent->UldManager.RootNode;
                while (lastChild->PrevSiblingNode != null) lastChild = lastChild->PrevSiblingNode;

                if (lastChild == (*vrTrgCursor))
                {
                    lastChild->NextSiblingNode->PrevSiblingNode = null;

                    (*vrTrgCursor)->AtkResNode.NextSiblingNode = null;
                    (*vrTrgCursor)->AtkResNode.ParentNode = null;

                    npComponent->UldManager.UpdateDrawNodeList();
                }
                else
                {
                    PluginLog.Error("RemoveVRCursor: lastChild != vrTargetCursor");
                }
            }
        }

        public static void UpdateVRCursorSize(AtkTextNode** vrTrgCursor)
        {
            if ((*vrTrgCursor) == null) return;

            (*vrTrgCursor)->FontSize = (byte)xivr.cfg.data.targetCursorSize;
            ushort outWidth = 0;
            ushort outHeight = 0;
            (*vrTrgCursor)->GetTextDrawSize(&outWidth, &outHeight);
            (*vrTrgCursor)->AtkResNode.SetWidth(outWidth);
            (*vrTrgCursor)->AtkResNode.SetHeight(outHeight);

            // explanation of these numbers
            // Some setup info:
            // 1. The ↓ character output from GetTextDrawSize is always 1:1 with the
            //    requested font. Font size 100 results in outWidth 100 and outHeight 100.
            // 2. The anchor point for text fields are the upper left corner of the frame.
            // 3. The hand-tuned position of the default font size 100 is x 90, y -23.
            // 
            // Adding the inverted delta offset (and div by 2 for x) correctly moves the ancor
            // from upper left to bottom center. However I noticed that as the font scales
            // up and down, the point of the arrow drifts slightly along the x and y. This
            // is the reason for the * 1.10 and * 1.15. This corrects for the drift and keeps
            // the point of the arrow exactly where it should be.

            const float DriftOffset_X = 1.10f;
            const float DriftOffset_Y = 1.15f;

            short xpos = (short)(90 + ((100 - outWidth) / 2 * DriftOffset_X));
            short ypos = (short)(-23 + (100 - outWidth) * DriftOffset_Y);
            (*vrTrgCursor)->AtkResNode.SetPositionShort(xpos, ypos);
        }

        public static void SetVRCursor(AtkTextNode** vrTrgCursor, NamePlateObject* nameplate)
        {
            // nothing to do!
            if (currentNPTarget == nameplate)
                return;

            if ((*vrTrgCursor) != null)
            {
                if (currentNPTarget != null)
                {
                    RemoveVRCursor(vrTrgCursor, currentNPTarget);
                    currentNPTarget = null;
                }

                if (nameplate != null)
                {
                    AddVRCursor(vrTrgCursor, nameplate);
                    currentNPTarget = nameplate;
                }
            }
        }
    }
}
