using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace ModMenus
{
    partial class ModMenusUtils
    {
        /// <summary>
        /// Returns true only after a gameplay scene is active and its game-only data exists.
        /// The title screen intentionally fails this check.
        /// </summary>
        public static bool IsGameplayReady()
        {
            Camera camera = Camera.main;
            if (camera == null)
            {
                return false;
            }

            mainScript main = camera.GetComponent<mainScript>();
            if (main == null || main.Data == null || !main.IsGameScene)
            {
                return false;
            }

            // mainScript.IsMainMenu() uses this same component as the distinction between
            // the title screen and a loaded game. Checking it directly avoids calling the
            // static helper while scene objects are still being constructed.
            return main.Data.GetComponent<SpecialEvents_Manager>() != null;
        }

        /// <summary>
        /// Installs or repairs the Mod Settings button on the active in-game settings tab.
        /// </summary>
        public static bool TryInstallSettingsButton()
        {
            if (!IsGameplayReady())
            {
                return false;
            }

            mainScript main = Camera.main.GetComponent<mainScript>();

            Tabs_Manager tabsManager = main.Data.GetComponent<Tabs_Manager>();
            if (tabsManager == null)
            {
                return false;
            }

            Tabs_Manager._tab settingsTab = tabsManager.GetTab(Tabs_Manager._tab._type.settings);
            if (settingsTab == null || settingsTab.Tab == null || !settingsTab.Tab.activeInHierarchy)
            {
                return false;
            }

            Transform settingsContainer = FindSettingsContainer(settingsTab.Tab.transform);
            if (settingsContainer == null)
            {
                return false;
            }

            // Mod Settings has one ordering anchor only: the vanilla Settings button.
            // Do not derive its location from childCount and do not anchor it to
            // Save & Quit/Main Menu, so later mods remain free to occupy that region.
            Transform settingsButton = FindSettingsButton(settingsContainer);
            if (settingsButton == null || settingsButton.parent == null)
            {
                return false;
            }

            settingsContainer = settingsButton.parent;

            Transform existingButton = FindNamedChild(settingsTab.Tab.transform, BUTTON_OBJ_NAME);
            if (existingButton != null)
            {
                ConfigureModMenuButton(existingButton.gameObject);
                PositionModMenuButton(existingButton, settingsButton);
                return true;
            }

            // Clone the exact vanilla Settings button that also serves as our anchor.
            GameObject modMenuButton = CloneButton(
                settingsButton.gameObject,
                settingsContainer,
                BUTTON_OBJ_NAME,
                BUTTON_LABEL,
                false,
                false);

            if (modMenuButton == null)
            {
                return false;
            }

            ConfigureModMenuButton(modMenuButton);
            PositionModMenuButton(modMenuButton.transform, settingsButton);
            return true;
        }

        /// <summary>
        /// Finds the settings list container using strict path first, then robust fallback.
        /// </summary>
        private static Transform FindSettingsContainer(Transform settingsRoot)
        {
            if (settingsRoot == null)
            {
                return null;
            }

            Transform container = settingsRoot.Find("ScrollRect/Container");
            if (container != null)
            {
                return container;
            }

            Transform[] descendants = settingsRoot.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < descendants.Length; i++)
            {
                Transform candidate = descendants[i];
                if (candidate == null)
                {
                    continue;
                }

                if (!string.Equals(candidate.name, "Container", StringComparison.Ordinal))
                {
                    continue;
                }

                if (candidate.GetComponent<VerticalLayoutGroup>() != null || candidate.GetComponent<GridLayoutGroup>() != null)
                {
                    return candidate;
                }
            }

            Button[] buttons = settingsRoot.GetComponentsInChildren<Button>(true);
            for (int i = 0; i < buttons.Length; i++)
            {
                Button candidate = buttons[i];
                if (candidate == null || candidate.gameObject == null || candidate.transform.parent == null)
                {
                    continue;
                }

                if (candidate.GetComponentInChildren<TextMeshProUGUI>(true) == null)
                {
                    continue;
                }

                Transform parent = candidate.transform.parent;
                if (parent.GetComponent<LayoutGroup>() != null)
                {
                    return parent;
                }
            }

            return null;
        }

        /// <summary>
        /// Finds a named descendant in a hierarchy using depth-first traversal.
        /// </summary>
        private static Transform FindNamedChild(Transform root, string childName)
        {
            if (root == null || string.IsNullOrEmpty(childName))
            {
                return null;
            }

            Transform[] descendants = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < descendants.Length; i++)
            {
                Transform candidate = descendants[i];
                if (candidate != null && string.Equals(candidate.name, childName, StringComparison.Ordinal))
                {
                    return candidate;
                }
            }

            return null;
        }

        /// <summary>
        /// Finds the vanilla Settings button by its stable GameObject name.
        /// This intentionally does not use Save &amp; Quit/Main Menu or any modded button
        /// as a fallback ordering anchor.
        /// </summary>
        private static Transform FindSettingsButton(Transform settingsContainer)
        {
            if (settingsContainer == null)
            {
                return null;
            }

            Transform direct = settingsContainer.Find(SETTINGS_BUTTON_OBJ_NAME);
            if (direct != null && direct.GetComponent<Button>() != null)
            {
                return direct;
            }

            Button[] buttons = settingsContainer.GetComponentsInChildren<Button>(true);
            for (int i = 0; i < buttons.Length; i++)
            {
                Button candidate = buttons[i];
                if (candidate == null || candidate.gameObject == null)
                {
                    continue;
                }

                if (string.Equals(candidate.gameObject.name, SETTINGS_BUTTON_OBJ_NAME, StringComparison.Ordinal))
                {
                    return candidate.transform;
                }
            }

            return null;
        }

        /// <summary>
        /// Repairs Mod Settings so it is immediately after the vanilla Settings button.
        /// Called both when creating the button and on every Settings-tab activation
        /// when an existing ModMenuButton is found.
        /// </summary>
        private static void PositionModMenuButton(Transform modMenuButton, Transform settingsButton)
        {
            if (modMenuButton == null || settingsButton == null || settingsButton.parent == null)
            {
                return;
            }

            Transform parent = settingsButton.parent;
            if (modMenuButton.parent != parent)
            {
                modMenuButton.SetParent(parent, false);
            }

            MoveImmediatelyAfter(modMenuButton, settingsButton);

            RectTransform settingsRect = parent as RectTransform;
            if (settingsRect != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(settingsRect);
            }

            Canvas.ForceUpdateCanvases();
        }

        private static void MoveImmediatelyAfter(Transform item, Transform anchor)
        {
            if (item == null || anchor == null || item == anchor || anchor.parent == null)
            {
                return;
            }

            Transform parent = anchor.parent;
            if (item.parent != parent)
            {
                item.SetParent(parent, false);
            }

            item.SetSiblingIndex(SiblingIndexAfter(item.GetSiblingIndex(), anchor.GetSiblingIndex(), parent.childCount));
        }

        /// <summary>
        /// Returns the sibling index that places an item immediately after its anchor.
        /// </summary>
        public static int SiblingIndexAfter(int itemIndex, int anchorIndex, int childCount)
        {
            int targetIndex = anchorIndex + 1;

            // Removing an item that started before the anchor shifts the anchor left.
            if (itemIndex < targetIndex)
            {
                targetIndex--;
            }

            return Mathf.Clamp(targetIndex, 0, childCount - 1);
        }

        /// <summary>
        /// Configures localized text and click action for the Mod Settings button.
        /// </summary>
        private static void ConfigureModMenuButton(GameObject modMenuButton)
        {
            if (modMenuButton == null)
            {
                return;
            }

            Transform textTransform = modMenuButton.transform.Find("Text");
            if (textTransform != null)
            {
                Lang_Button languageBinding = textTransform.GetComponent<Lang_Button>();
                if (languageBinding != null)
                {
                    languageBinding.Constant = BUTTON_LABEL;
                }

                TextMeshProUGUI label = textTransform.GetComponent<TextMeshProUGUI>();
                if (label != null)
                {
                    label.text = Language.Data.TryGetValue(BUTTON_LABEL, out string localized) ? localized : BUTTON_LABEL;
                }
            }

            Button button = modMenuButton.GetComponent<Button>();
            if (button == null)
            {
                return;
            }

            button.onClick = new Button.ButtonClickedEvent();
            button.onClick.AddListener(() =>
            {
                if (!IsGameplayReady())
                {
                    return;
                }

                if (GenerateMenuPopup() != null)
                {
                    PopupManager.OpenPopup(MOD_MENU_POPUP);
                }
            });
        }
    }
}
