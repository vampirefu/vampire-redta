using Microsoft.Xna.Framework.Graphics;
using Rampastring.XNAUI;
using Rampastring.XNAUI.XNAControls;
using System;
using System.Collections.Generic;
namespace ClientGUI
{
    public enum PlayerSlotState
    {
        Empty,
        Unavailable,
        AI,
        NotReady,
        Ready,
        InGame,
        Warning,
        Error
    }

    public class XNAPlayerSlotIndicator : XNAIndicator<PlayerSlotState>
    {
        public static new Dictionary<PlayerSlotState, Texture2D> Textures { get; set; }

        public ToolTip ToolTip { get; set; }

        public XNAPlayerSlotIndicator(WindowManager windowManager) : base(windowManager, Textures)
        {
        }

        public static void LoadTextures()
        {
            Textures = new Dictionary<PlayerSlotState, Texture2D>()
            {
                { PlayerSlotState.Empty, AssetLoader.LoadTextureUncached("statusEmpty.png") },
                { PlayerSlotState.Unavailable, AssetLoader.LoadTextureUncached("statusUnavailable.png") },
                { PlayerSlotState.AI, AssetLoader.LoadTextureUncached("statusAI.png") },
                { PlayerSlotState.NotReady, AssetLoader.LoadTextureUncached("statusClear.png") },
                { PlayerSlotState.Ready, AssetLoader.LoadTextureUncached("statusOk.png") },
                { PlayerSlotState.InGame, AssetLoader.LoadTextureUncached("statusInProgress.png") },
                { PlayerSlotState.Warning, AssetLoader.LoadTextureUncached("statusWarning.png") },
                { PlayerSlotState.Error, AssetLoader.LoadTextureUncached("statusError.png") }
            };
        }

        public override void Initialize()
        {
            base.Initialize();

            ToolTip = new ToolTip(WindowManager, this);
        }

        public override void SwitchTexture(PlayerSlotState key)
        {
            base.SwitchTexture(key);

            switch (key)
            {
                case PlayerSlotState.Empty:
                    ToolTip.Text = "该位置无玩家";
                    break;

                case PlayerSlotState.Unavailable:
                    ToolTip.Text = "该位置不可用。";
                    break;

                case PlayerSlotState.AI:
                    ToolTip.Text = "该玩家由电脑控制";
                    break;

                case PlayerSlotState.NotReady:
                    ToolTip.Text = "该玩家没有准备";
                    break;

                case PlayerSlotState.Ready:
                    ToolTip.Text = "该玩家已准备。";
                    break;

                case PlayerSlotState.InGame:
                    ToolTip.Text = "该玩家正在游戏中。";
                    break;

                case PlayerSlotState.Warning:
                    ToolTip.Text = "该玩家存在可能影响游戏的问题。";
                    break;

                case PlayerSlotState.Error:
                    ToolTip.Text = "该玩家存在严重问题。";
                    break;
            }
        }
    }
}
