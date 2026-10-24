using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

namespace ModMenus
{
    partial class ModMenusUtils
    {
        /// <summary>
        /// Manages the mod menu functionality: the tabs, the page shown, and Apply and Cancel.
        /// </summary>
        public partial class ModMenuManager : MonoBehaviour
        {
            /// <summary>
            /// The mod whose tab the player last showed, so the menu reopens on it, even after the menu is
            /// built again for another game.
            /// </summary>
            public static string lastModName;

            /// <summary>The menu while it's open, for MenuValue; null while it's closed.</summary>
            internal static ModMenuManager open;

            public ScrollRect scroll;
            public ScrollRect tabScroll;
            public List<ModMenuTab> tabs;
            public ModMenuTab selected;

            /// <summary>
            /// Frames to wait before scrolling the tabs to the one shown: tab names wrap in the first frames.
            /// </summary>
            private int framesToShowTab;

            /// <summary>
            /// While every row is being set at once, rows with enabledIf are shown or hidden once at the end
            /// instead of after each one.
            /// </summary>
            private bool settingAll;

            /// <summary>The settings whose enabledIf loop has been logged, so each loop is logged once.</summary>
            private HashSet<string> loopsLogged;

            /// <summary>
            /// Called when the mod menu is enabled. Every page shows the saved settings, whether or not
            /// its tab is shown, and whether its "Use in all saves" box is ticked; what was changed and not
            /// applied is gone.
            /// </summary>
            public void OnEnable()
            {
                // Set first: the rows' enabledIf variables are read below, and a mod may answer one from MenuValue
                open = this;
                // A list left open when the menu closed would sit where it was, apart from its row,
                // after the menu reopens scrolled to the top
                CloseDropdownLists();
                settingAll = true;
                foreach (ModMenuItem item in GetComponentsInChildren<ModMenuItem>(true))
                {
                    item.ShowSaved();
                }
                settingAll = false;
                ShowSharedTabs();
                RefreshConditions();
                RefreshClashes();
                if (scroll != null)
                {
                    scroll.verticalNormalizedPosition = 1;
                }
                RenderTabs();
                framesToShowTab = 2;
            }

            /// <summary>
            /// Called when the menu closes: MenuValue has nothing to read until it opens again, and a key row
            /// waiting for a key stops waiting.
            /// </summary>
            public void OnDisable()
            {
                if (open == this)
                {
                    open = null;
                }
                ModMenuItem.StopAnyListening();
            }

            public void LateUpdate()
            {
                if (framesToShowTab <= 0 || --framesToShowTab > 0)
                {
                    return;
                }
                ScrollToTab(selected);
            }

            /// <summary>
            /// Shows a tab's page and hides the others. Lists are closed first: an open list isn't in its page.
            /// </summary>
            public void ShowPage(ModMenuTab tab)
            {
                if (tab == null || tab == selected)
                {
                    return;
                }
                CloseDropdownLists();
                foreach (ModMenuTab other in tabs)
                {
                    other.page.SetActive(other == tab);
                }
                selected = tab;
                lastModName = tab.modName;
                RenderTabs();
                if (scroll != null)
                {
                    scroll.verticalNormalizedPosition = 1;
                    scroll.StopMovement();
                }
            }

            /// <summary>
            /// Draws each tab as shown or not.
            /// </summary>
            public void RenderTabs()
            {
                if (tabs == null)
                {
                    return;
                }
                foreach (ModMenuTab tab in tabs)
                {
                    tab.Render(tab == selected);
                }
            }

            /// <summary>
            /// Scrolls the tab column so a tab shows whole.
            /// </summary>
            public void ScrollToTab(ModMenuTab tab)
            {
                if (tab == null || tabScroll == null || tabScroll.content == null || tabScroll.viewport == null)
                {
                    return;
                }
                RectTransform tabRect = (RectTransform)tab.transform;
                float height = tabRect.rect.height;

                // The column puts each tab's top-left corner at its place down the column
                float top = -tabRect.anchoredPosition.y - height * (1 - tabRect.pivot.y);
                tabScroll.verticalNormalizedPosition = ScrollPositionShowing(tabScroll.content.rect.height, tabScroll.viewport.rect.height, top, top + height, tabScroll.verticalNormalizedPosition);
            }

            /// <summary>
            /// Closes every open dropdown list.
            /// </summary>
            public void CloseDropdownLists()
            {
                foreach (ModMenuItem item in GetComponentsInChildren<ModMenuItem>(true))
                {
                    item.CloseList();
                }
            }

            /// <summary>
            /// Sets the shown tab's settings back to their defaults, except rows with "reset": false, including
            /// rows hidden by enabledIf. Nothing is saved: Apply saves the defaults, Cancel throws them away.
            /// </summary>
            public void OnReset()
            {
                if (selected == null || selected.page == null)
                {
                    return;
                }
                CloseDropdownLists();
                settingAll = true;
                foreach (ModMenuItem item in selected.page.transform.GetComponentsInChildren<ModMenuItem>(true))
                {
                    item.ShowDefault();
                }
                settingAll = false;
                RefreshConditions();
                RefreshClashes();
            }

            /// <summary>
            /// Called when the player changes a setting, which may show or hide rows with enabledIf, and give
            /// keys a clash or take one away.
            /// </summary>
            public void OnSettingChanged()
            {
                if (!settingAll)
                {
                    RefreshConditions();
                    RefreshClashes();
                }
            }

            /// <summary>
            /// Shows each row with enabledIf while its setting is on, on every page (see ConditionMet), and hides
            /// it otherwise; the rows under it move up. A setting's value is its row's, changed or not, on any
            /// tab; or, if no row has it, its saved value. Hidden rows keep their value, and Apply saves it.
            /// </summary>
            public void RefreshConditions()
            {
                ModMenuCondition[] conditions = GetComponentsInChildren<ModMenuCondition>(true);
                if (conditions.Length == 0)
                {
                    return;
                }

                Dictionary<string, string> values = new();
                Dictionary<string, string> rowConditions = new();
                foreach (ModMenuItem item in GetComponentsInChildren<ModMenuItem>(true))
                {
                    if (item.varID == null || values.ContainsKey(item.varID))
                    {
                        continue;
                    }
                    values[item.varID] = item.CurrentValue;
                    ModMenuCondition own = item.GetComponent<ModMenuCondition>();
                    rowConditions[item.varID] = own != null ? own.varID : null;
                }
                string ValueOf(string varID) => values.TryGetValue(varID, out string value) ? value : variables.Get(varID);
                string ConditionOf(string varID) => rowConditions.TryGetValue(varID, out string condition) ? condition : null;

                foreach (ModMenuCondition condition in conditions)
                {
                    bool loop = false;
                    bool shown = ConditionMet(condition.varID, ValueOf, ConditionOf, new HashSet<string>(), ref loop);
                    if (loop)
                    {
                        // No row is lost for good: a row in a loop could never be shown again once hidden
                        shown = true;
                        if ((loopsLogged ??= new HashSet<string>()).Add(condition.varID))
                        {
                            Debug.LogWarning("[ModMenus] The enabledIf settings leading from " + condition.varID + " lead back to themselves, so their rows are always shown.");
                        }
                    }
                    if (!shown)
                    {
                        // An open list is in the dropdown layer, not in its row, and would stay there
                        ModMenuItem item = condition.GetComponent<ModMenuItem>();
                        if (item != null)
                        {
                            item.CloseList();
                        }
                    }
                    condition.gameObject.SetActive(shown);
                }
                RefreshSections();
            }

            /// <summary>
            /// Handles the cancel action for the mod menu.
            /// </summary>
            public void OnCancel()
            {
                CloseDropdownLists();
                PopupManager.Close_();
            }

            /// <summary>
            /// Handles the apply action for the mod menu, saving every setting on every page, shown or not, and
            /// the settings of the tabs whose "Use in all saves" box is ticked (see SaveSharedTabs). Secrets go
            /// to their own files (see ApplySecret), and mods are told which changed once everything is saved.
            /// </summary>
            public void OnApply()
            {
                CloseDropdownLists();
                List<string> changedSecrets = new();
                foreach(ModMenuItem item in GetComponentsInChildren<ModMenuItem>(true))
                {
                    if (item.secretID != null)
                    {
                        if (item.ApplySecret())
                        {
                            changedSecrets.Add(item.secretID);
                        }
                        continue;
                    }
                    // A number field still being typed in hasn't been checked yet
                    item.Validate();
                    variables.Set(item.varID, item.CurrentValue);
                }
                // After the save's own copy: if the file can't be written, this save still has every change
                SaveSharedTabs();
                RaiseSecretsChanged(changedSecrets);
                PopupManager.Close_();
            }

            /// <summary>
            /// Ticks the "Use in all saves" box of each tab whose settings are shared.
            /// </summary>
            private void ShowSharedTabs()
            {
                Dictionary<string, Dictionary<string, string>> shared = SharedSettings.Load();
                foreach (ModMenuScopeRow row in GetComponentsInChildren<ModMenuScopeRow>(true))
                {
                    row.Show(shared.ContainsKey(row.tabID));
                }
            }

            /// <summary>
            /// Keeps each ticked tab's settings for every save; an unticked tab's are no longer shared, and each
            /// save keeps the values it has. Secret rows have no varID, so they're never here. Shared settings
            /// no row shows (their mod is off) are kept, and so are tabs this menu doesn't have.
            /// </summary>
            private void SaveSharedTabs()
            {
                ModMenuScopeRow[] rows = GetComponentsInChildren<ModMenuScopeRow>(true);
                if (rows.Length == 0)
                {
                    return;
                }
                HashSet<string> shown = new();
                foreach (ModMenuItem item in GetComponentsInChildren<ModMenuItem>(true))
                {
                    shown.Add(item.varID);
                }

                Dictionary<string, Dictionary<string, string>> shared = SharedSettings.Load();
                foreach (ModMenuScopeRow row in rows)
                {
                    shared.TryGetValue(row.tabID, out Dictionary<string, string> before);
                    shared.Remove(row.tabID);
                    if (!row.ticked)
                    {
                        continue;
                    }
                    Dictionary<string, string> values = new();
                    if (before != null)
                    {
                        foreach (KeyValuePair<string, string> setting in before)
                        {
                            if (!shown.Contains(setting.Key))
                            {
                                values[setting.Key] = setting.Value;
                            }
                        }
                    }
                    foreach (ModMenuItem item in row.page.GetComponentsInChildren<ModMenuItem>(true))
                    {
                        if (item.varID != null)
                        {
                            values[item.varID] = item.CurrentValue;
                        }
                    }
                    shared[row.tabID] = values;
                }
                SharedSettings.Save(shared);
            }

        }
    }
}
