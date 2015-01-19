/* Copyright © 2013-2015, Elián Hanisch <lambdae2@gmail.com>
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU Lesser General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU Lesser General Public License for more details.
 *
 * You should have received a copy of the GNU Lesser General Public License
 * along with this program.  If not, see <http://www.gnu.org/licenses/>.
 */

using System;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

namespace RCSBuildAid
{
    public class MainWindow : MonoBehaviour
    {
        int winID;
        Rect winRect;
        Rect winCBodyListRect;
        bool modeSelect;
        bool softLock;
        bool settings;
        bool shortcut_selection;
        const string title = "RCS Build Aid v0.5.4";
        // Analysis disable ConvertToConstant.Local
        int minWidth = 184;
        int maxWidth = 184;
        int minHeight = 52;
        int maxHeight = 52;
        int minimizedWidth = 184;
        int minimizedHeight = 26;
        // Analysis restore ConvertToConstant.Local

        public static bool cBodyListEnabled;
        public static PluginMode cBodyListMode;
        public static CelestialBody body;

        public static Style style;
        public static event Action onDrawToggleableContent;
        public static event Action onDrawModeContent;

        Dictionary<PluginMode, string> menuTitles = new Dictionary<PluginMode, string> {
            { PluginMode.Attitude, "Attitude"    },
            { PluginMode.RCS     , "Translation" },
            { PluginMode.Engine  , "Engines"     },
        };

        bool minimized { 
            get { return Settings.menu_minimized; }
            set { Settings.menu_minimized = value; }
        }

        void Awake ()
        {
            winID = gameObject.GetInstanceID ();
            winRect = new Rect (Settings.window_x, Settings.window_y, minWidth, minHeight);
            winCBodyListRect = new Rect ();
            Load ();
            onDrawModeContent = null;
            onDrawToggleableContent = null;
            onDrawToggleableContent += gameObject.AddComponent<MenuMass> ().DrawContent;
            onDrawToggleableContent += gameObject.AddComponent<MenuResources> ().DrawContent;
            onDrawToggleableContent += gameObject.AddComponent<MenuMarkers> ().DrawContent;
            RCSBuildAid.events.onModeChange += gameObject.AddComponent<MenuTranslation> ().onModeChange;
            RCSBuildAid.events.onModeChange += gameObject.AddComponent<MenuEngines> ().onModeChange;
            RCSBuildAid.events.onModeChange += gameObject.AddComponent<MenuAttitude> ().onModeChange;
            RCSBuildAid.events.onSave += Save;
#if DEBUG
            onDrawToggleableContent += gameObject.AddComponent<MenuDebug> ().DrawContent;
#endif
        }

        void Start ()
        {
            body = FlightGlobals.Bodies.Find(b => b.name == Settings.engine_cbody);
        }

        void Load ()
        {
            /* check if within screen */
            winRect.x = Mathf.Clamp (winRect.x, 0, Screen.width - maxWidth);
            winRect.y = Mathf.Clamp (winRect.y, 0, Screen.height - maxHeight);
        }

        void Save ()
        {
            Settings.window_x = (int)winRect.x;
            Settings.window_y = (int)winRect.y;
        }

        void OnGUI ()
        {
            switch (HighLogic.LoadedScene) {
            case GameScenes.EDITOR:
                break;
            default:
                /* don't show window during scene changes */
                return;
            }

            if (style == null) {
                style = new Style ();
            }

            if (RCSBuildAid.Enabled) {
                if (minimized) {
                    winRect.height = minimizedHeight;
                    winRect.width = minimizedWidth;
                    winRect = GUI.Window (winID, winRect, drawWindowMinimized, title, style.mainWindowMinimized);
                } else {
                    if (Event.current.type == EventType.Layout) {
                        winRect.height = minHeight;
                        winRect.width = minWidth;
                    }
                    winRect = GUILayout.Window (winID, winRect, drawWindow, title, style.mainWindow);

                    cBodyListEnabled = cBodyListEnabled && (RCSBuildAid.mode == cBodyListMode);
                    if (cBodyListEnabled) {
                        if (Event.current.type == EventType.Layout) {
                            if ((winRect.x + winRect.width + style.cBodyListWidth + 5) > Screen.width) {
                                winCBodyListRect.x = winRect.x - style.cBodyListWidth - 5;
                            } else {
                                winCBodyListRect.x = winRect.x + winRect.width + 5;
                            }

                            winCBodyListRect.y = winRect.y;
                            winCBodyListRect.width = style.cBodyListWidth;
                            winCBodyListRect.height = minHeight;
                        }
                        winCBodyListRect = GUILayout.Window (winID + 1, winCBodyListRect, 
                                                             drawBodyListWindow,
                                                             "Celestial bodies", GUI.skin.box);
                    } 
                }
            }
            if (Event.current.type == EventType.Repaint) {
                setEditorLock ();
            }

            debug ();
        }

        void drawWindowMinimized (int ID)
        {
            minimizeButton();
            GUI.DragWindow ();
        }

        bool selectModeButton ()
        {
            bool value;
            if (modeSelect) {
                value = GUILayout.Button ("Select mode", style.mainButton);
            } else {
                GUILayout.BeginHorizontal ();
                {
                    nextModeButton ("<", -1);
                    if (RCSBuildAid.mode == PluginMode.none) {
                        value = GUILayout.Button ("Select mode", style.mainButton);
                    } else {
                        value = GUILayout.Button (menuTitles [RCSBuildAid.mode], style.activeButton);
                    }
                    nextModeButton (">", 1);
                }
                GUILayout.EndHorizontal ();
            }
            return value;
        }

        void nextModeButton(string modeName, int step) {
            if (GUILayout.Button (modeName, style.mainButton, GUILayout.Width (20))) {
                const int n = 3; // max number of modes FIXME, is pain to have it hardcoded.
                int i = (int)RCSBuildAid.mode + step;
                if (i < 1) {
                    i = n;
                } else if (i > n) {
                    i = 1;
                }
                RCSBuildAid.events.SetMode ((PluginMode)i);
            }
        }

        void drawWindow (int ID)
        {
            if (minimizeButton () && minimized) {
                return;
            }
            settingsButton ();
            if (settings) {
                drawSettings ();
                GUI.DragWindow ();
                return;
            }
            GUILayout.BeginVertical ();
            {
                if (selectModeButton ()) {
                    modeSelect = !modeSelect;
                }
                if (!modeSelect) {
                    if (onDrawModeContent != null) {
                        onDrawModeContent ();
                    }
                } else {
                    drawModeSelectList ();
                }

                if (onDrawToggleableContent != null) {
                    onDrawToggleableContent ();
                }
            }
            GUILayout.EndVertical ();
            GUI.DragWindow ();
        }

        void drawModeSelectList ()
        {
            GUILayout.BeginVertical (GUI.skin.box);
            {
                const int n = 3; /* total number of modes */
                int r = Mathf.CeilToInt (n / 2f);
                int i = 1;

                GUILayout.BeginHorizontal ();
                {
                    while (i <= n) {
                        GUILayout.BeginVertical ();
                        {
                            for (int j = 0; (j < r) && (i <= n); j++) {
                                if (GUILayout.Button (menuTitles [(PluginMode)i], style.clickLabel)) {
                                    modeSelect = false;
                                    RCSBuildAid.events.SetMode ((PluginMode)i);
                                }
                                i++;
                            }
                        }
                        GUILayout.EndVertical ();
                    }
                }
                GUILayout.EndHorizontal ();
                if (GUILayout.Button ("None", style.clickLabelCenter)) {
                    modeSelect = false;
                    RCSBuildAid.events.SetMode (PluginMode.none);
                }
            }
            GUILayout.EndVertical ();
        }

        bool minimizeButton ()
        {
            if (GUI.Button (new Rect (winRect.width - 15, 3, 12, 12), String.Empty, style.tinyButton)) {
                minimized = !minimized;
                minimizedWidth = (int)winRect.width;
                return true;
            }
            return false;
        }

        bool settingsButton ()
        {
            if (GUI.Button (new Rect (winRect.width - 30, 3, 12, 12), "s", style.tinyButton)) {
                settings = !settings;
                return true;
            }
            return false;
        }

        void drawSettings ()
        {
            GUILayout.Label ("Settings", style.resourceTableName);
           
            GUI.enabled = Settings.toolbar_plugin_loaded;
            bool applauncher = Settings.applauncher;
            applauncher = GUILayout.Toggle (applauncher, "Use application launcher");
            if (applauncher != Settings.applauncher) {
                Settings.applauncher = applauncher;
                if (applauncher) {
                    AppLauncher.instance.addButton ();
                } else {
                    AppLauncher.instance.removeButton ();
                    if (!Settings.toolbar_plugin) {
                        Settings.setupToolbar (true);
                    }
                }
            }
            GUI.enabled = Settings.toolbar_plugin_loaded && Settings.applauncher;
            bool toolbar = Settings.toolbar_plugin;
            toolbar = GUILayout.Toggle (toolbar, "Use blizzy's toolbar");
            if (Settings.toolbar_plugin != toolbar) {
                Settings.setupToolbar (toolbar);
            }
            GUI.enabled = true;
            Settings.action_screen = GUILayout.Toggle (Settings.action_screen, "Show in Action Groups");
            if (shortcut_selection) {
                if (GUILayout.Button ("Press any key", GUI.skin.button)) {
                    shortcut_selection = false;
                }
                if (Event.current.isKey) {
                    if (Event.current.keyCode == KeyCode.Escape) {
                        shortcut_selection = false;
                        Settings.shortcut_key = KeyCode.None;
                    } else if (Event.current.type == EventType.KeyUp) {
                        shortcut_selection = false;
                        Settings.shortcut_key = Event.current.keyCode;
                    }
                }
            } else {
                if (GUILayout.Button ("Shortcut: " + Settings.shortcut_key.ToString())) {
                    shortcut_selection = true;
                }
            }
        }

        void drawBodyListWindow (int ID)
        {
            GUILayout.Space(GUI.skin.box.lineHeight + 4);
            GUILayout.BeginVertical ();
            {
                celestialBodyRecurse(Planetarium.fetch.Sun, 5);
            }
            GUILayout.EndVertical();
        }

        void celestialBodyRecurse (CelestialBody body, int padding)
        {
            style.listButton.padding.left = padding;
            if (GUILayout.Button (body.name, style.listButton)) {
                cBodyListEnabled = false;
                MainWindow.body = body;
                Settings.engine_cbody = body.name;
            }

            foreach (CelestialBody b in body.orbitingBodies) {
                celestialBodyRecurse(b, padding + 10);
            }
        }

        public static void directionButton()
        {
            if (GUILayout.Button (RCSBuildAid.Direction.ToString (), MainWindow.style.smallButton)) {
                int i = (int)RCSBuildAid.Direction;
                i = loopIndexSelect (1, 6, i);
                RCSBuildAid.Direction = (Direction)i;
            }
        }

        public static int loopIndexSelect(int min_index, int max_index, int i)
        {
            if (Event.current.button == 0) {
                i += 1;
                if (i > max_index) {
                    i = min_index;
                }
            } else if (Event.current.button == 1) {
                i -= 1;
                if (i < min_index) {
                    i = max_index;
                }
            }
            return i;
        }

        public static void referenceButton ()
        {
            if (GUILayout.Button (RCSBuildAid.referenceMarker.ToString(), MainWindow.style.smallButton)) {
                selectNextReference ();
            } else if (!RCSBuildAid.isMarkerVisible (RCSBuildAid.referenceMarker)) {
                selectNextReference ();
            }
        }

        static void selectNextReference ()
        {
            bool[] array = { 
                RCSBuildAid.isMarkerVisible (MarkerType.CoM), 
                RCSBuildAid.isMarkerVisible (MarkerType.DCoM),
                RCSBuildAid.isMarkerVisible (MarkerType.ACoM)
            };
            if (!array.Any (o => o)) {
                return;
            }
            int i = (int)RCSBuildAid.referenceMarker;
            bool found = false;
            for (int j = 0; j < 3; j++) {
                i = loopIndexSelect (0, 2, i);
                if (array [i]) {
                    found = true;
                    break;
                }
            }
            if (found) {
                RCSBuildAid.SetReferenceMarker ((MarkerType)i);
            }
        }

        public static string timeFormat (float seconds)
        {
            int min = (int)seconds / 60;
            int sec = (int)seconds % 60;
            return String.Format("{0:D}m {1:D}s", min, sec);
        }

        bool isMouseOver ()
        {
            var position = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y);
            if (winRect.Contains (position)) {
                return true;
            }
            return cBodyListEnabled && winCBodyListRect.Contains (position);
        }

        /* Whenever we mouseover our window, we need to lock the editor so we don't pick up
         * parts while dragging the window around */
        void setEditorLock ()
        {
            if (RCSBuildAid.Enabled) {
                bool mouseOver = isMouseOver ();
                if (mouseOver && !softLock) {
                    softLock = true;
                    const ControlTypes controlTypes = ControlTypes.CAMERACONTROLS 
                                                    | ControlTypes.EDITOR_ICON_HOVER 
                                                    | ControlTypes.EDITOR_ICON_PICK 
                                                    | ControlTypes.EDITOR_PAD_PICK_PLACE 
                                                    | ControlTypes.EDITOR_PAD_PICK_COPY 
                                                    | ControlTypes.EDITOR_EDIT_STAGES 
                                                    | ControlTypes.EDITOR_GIZMO_TOOLS
                                                    | ControlTypes.EDITOR_ROOT_REFLOW;

                    InputLockManager.SetControlLock (controlTypes, "RCSBuildAidLock");
                } else if (!mouseOver && softLock) {
                    softLock = false;
                    InputLockManager.RemoveControlLock("RCSBuildAidLock");
                }
            } else if (softLock) {
                softLock = false;
                InputLockManager.RemoveControlLock("RCSBuildAidLock");
            }
        }

        /*
         * Debug stuff
         */

        [Conditional("DEBUG")]
        void debug ()
        {
            if (Input.GetKeyDown(KeyCode.Space)) {
                print (winRect.ToString ());
            }
        }

    }
}
