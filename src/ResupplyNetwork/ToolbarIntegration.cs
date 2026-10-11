using System;
using KSP.UI.Screens;
using ToolbarControl_NS;
using UnityEngine;

namespace ResupplyNetwork
{
    /// <summary>
    /// 按钮优先注册到 ToolbarController（玩家可在其设置页里选 Blizzy 或 stock），
    /// 注册失败时退回 KSP 原生 ApplicationLauncher。
    /// </summary>
    public static class ToolbarIntegration
    {
        public const string ModId = "ResupplyNetwork_NS";
        public const string ModName = "补给网 Resupply Network";
        private const string ButtonId = "resupplyNetworkButton";
        private const string IconLarge = "ResupplyNetwork/PluginData/Textures/icon_38";
        private const string IconSmall = "ResupplyNetwork/PluginData/Textures/icon_24";

        private static ToolbarControl _toolbar;
        private static ApplicationLauncherButton _stock;

        public static bool Register()
        {
            try
            {
                return ToolbarControl.RegisterMod(ModId, ModName);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[ResupplyNetwork] ToolbarControl.RegisterMod 失败: " + e.Message);
                return false;
            }
        }

        public static bool CreateButton(GameObject host, Action onTrue, Action onFalse)
        {
            try
            {
                _toolbar = host.AddComponent<ToolbarControl>();
                _toolbar.AddToAllToolbars(
                    new ToolbarControl.TC_ClickHandler(() => onTrue()),
                    new ToolbarControl.TC_ClickHandler(() => onFalse()),
                    ApplicationLauncher.AppScenes.ALWAYS,
                    ModId, ButtonId, IconLarge, IconSmall, ModName);
                Debug.Log("[ResupplyNetwork] 已在 ToolbarController 注册按钮");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[ResupplyNetwork] ToolbarController 注册按钮失败，改用原生工具栏: " + e.Message);
                return CreateStockButton(onTrue, onFalse);
            }
        }

        private static bool CreateStockButton(Action onTrue, Action onFalse)
        {
            try
            {
                if (ApplicationLauncher.Instance == null) return false;
                Texture2D tex = GameDatabase.Instance.GetTexture(IconLarge, false);
                _stock = ApplicationLauncher.Instance.AddModApplication(
                    new Callback(() => onTrue()),
                    new Callback(() => onFalse()),
                    null, null, null, null,
                    ApplicationLauncher.AppScenes.ALWAYS,
                    tex);
                Debug.Log("[ResupplyNetwork] 已在原生 ApplicationLauncher 注册按钮");
                return _stock != null;
            }
            catch (Exception e)
            {
                Debug.LogError("[ResupplyNetwork] 原生工具栏注册也失败: " + e);
                return false;
            }
        }

        public static void SetButtonActive(bool active)
        {
            try
            {
                if (_stock != null)
                {
                    if (active) _stock.SetTrue(false);
                    else _stock.SetFalse(false);
                }
                if (_toolbar != null)
                {
                    if (active) _toolbar.SetTrue(false);
                    else _toolbar.SetFalse(false);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[ResupplyNetwork] 同步按钮状态失败: " + e.Message);
            }
        }
    }
}
