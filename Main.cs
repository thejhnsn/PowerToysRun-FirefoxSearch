// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using ManagedCommon;
using Microsoft.Data.Sqlite;
using Microsoft.PowerToys.Settings.UI.Library;
using System.Globalization;
using System.Windows.Controls;
using Wox.Plugin;
using Wox.Plugin.Common;
using Wox.Plugin.Logger;

namespace Community.PowerToys.Run.Plugin.FirefoxSearch
{
    public class Main : IPlugin, IPluginI18n, IContextMenu, ISettingProvider, IReloadable, IDisposable, IDelayedExecutionPlugin
    {
        private const string Setting = nameof(Setting);

        private PluginInitContext? _context;

        private string? _iconPath;

        private bool _disposed;

        public string Name => Properties.Resources.plugin_name;

        public string Description => Properties.Resources.plugin_description;

        public static string PluginID => "F0523631B9D64908A8CCD76C52A03DDF";

        private int _maxResults = 40;

        private int _inclusions = 0;

        private static readonly string _path = Environment.ExpandEnvironmentVariables(@"%APPDATA%\Mozilla\Firefox\Profiles");

        private int _profiles;

        private static readonly string[] _sqlQueries =
        [
            @"
                SELECT bm.title, p.url, p.last_visit_date, 1 as favorite
                FROM moz_bookmarks AS bm, moz_places AS p 
                WHERE bm.fk = p.id AND (bm.title LIKE '%' || @search || '%' OR p.url LIKE '%' || @search || '%')
                UNION 
                SELECT p.title, p.url, p.last_visit_date, 0 as favorite
                FROM moz_historyvisits AS h, moz_places as p
                WHERE h.place_id = p.id AND (p.title LIKE '%' || @search || '%' OR p.url LIKE '%' || @search || '%')
                ORDER BY favorite DESC, p.last_visit_date DESC, bm.title ASC, p.url ASC
                LIMIT @maxResults
            ",
            @"
                SELECT bm.title, p.url, p.last_visit_date
                FROM moz_bookmarks AS bm, moz_places AS p 
                WHERE bm.fk = p.id AND (bm.title LIKE '%' || @search || '%' OR p.url LIKE '%' || @search || '%')
                ORDER BY p.last_visit_date DESC, bm.title ASC, p.url ASC
                LIMIT @maxResults
            ",
            @"
                SELECT p.title, p.url, p.last_visit_date
                FROM moz_historyvisits AS h, moz_places as p
                WHERE h.place_id = p.id AND (p.title LIKE '%' || @search || '%' OR p.url LIKE '%' || @search || '%')
                ORDER BY p.last_visit_date DESC, p.title ASC, p.url ASC
                LIMIT @maxResults
            ",
        ];

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        public IEnumerable<PluginAdditionalOption> AdditionalOptions => new List<PluginAdditionalOption>()
        {
            new()
            {
                Key = "Inclusions",
                DisplayLabel = Properties.Resources.display_label_inclusions,
                PluginOptionType = PluginAdditionalOption.AdditionalOptionType.Combobox,
                ComboBoxItems =
                [
                    new(Properties.Resources.string_bookmark, "1"),
                    new(Properties.Resources.string_history, "2"),
                    new(Properties.Resources.string_bookmark_history, "0")
                ],
                ComboBoxValue = _inclusions
            },
            new()
            {
                Key = "MaxResults",
                DisplayLabel = Properties.Resources.display_label_max_results,
                PluginOptionType = PluginAdditionalOption.AdditionalOptionType.Numberbox,
                NumberValue = _maxResults,
            },
        };

        public List<ContextMenuResult> LoadContextMenus(Result selectedResult)
        {
            return [ new ContextMenuResult
            {
                PluginName = Name,
                Title = "Copy Title",
                Glyph = "\xE8C8", // Glyph for Copy
                FontFamily = "Segoe MDL2 Assets",
                AcceleratorKey = System.Windows.Input.Key.C,
                AcceleratorModifiers = System.Windows.Input.ModifierKeys.Control,
                Action = _ =>
                {
                    System.Windows.Clipboard.SetText(selectedResult.Title);
                    return true;
                },
            }, new ContextMenuResult
            {
                PluginName = Name,
                Title = "Copy URL",
                Glyph = "\xE71B", // Glyph for Link
                FontFamily = "Segoe MDL2 Assets",
                AcceleratorKey = System.Windows.Input.Key.C,
                AcceleratorModifiers = System.Windows.Input.ModifierKeys.Control,
                Action = _ =>
                {
                    System.Windows.Clipboard.SetText(selectedResult.SubTitle);
                    return true;
                },
            } ];
        }

        public List<Result> Query(Query query)
        {
            ArgumentNullException.ThrowIfNull(query);

            var results = new List<Result>();

            return results;
        }

        public List<Result> Query(Query query, bool delayedExecution)
        {
            ArgumentNullException.ThrowIfNull(query);

            var results = new List<Result>();

            // Empty query - show plugin name and description
            if (string.IsNullOrEmpty(query.Search))
            {
                results.Add(new Result
                {
                    Title = Name,
                    SubTitle = Description,
                    QueryTextDisplay = string.Empty,
                    IcoPath = _iconPath,
                    Action = _ =>
                    {
                        return true;
                    },
                });
                return results;
            }
            _profiles = System.IO.Directory.GetDirectories(_path).Length;
            for (int i = 0; i < _profiles; i++)
            {
                string profile = System.IO.Directory.GetDirectories(_path)[i];
                string bookmarks = System.IO.Path.Combine(profile, "places.sqlite");

                if (!System.IO.File.Exists(bookmarks))
                {
                    continue;
                }
                using (var db = new SqliteConnection($"Filename={bookmarks}"))
                {
                    db.Open();
                    // Build and execute query
                    SqliteCommand command = new(_sqlQueries[_inclusions], db);
                    command.Parameters.AddWithValue("@search", query.Search);
                    command.Parameters.AddWithValue("@maxResults", _maxResults);
                    var reader = command.ExecuteReader();

                    while (reader.Read())
                    {
                        string title = "No title"; // default value
                        if (!reader.IsDBNull(0))
                        {
                            title = reader.GetString(0);
                        }
                        if (reader.IsDBNull(1))
                        {
                            continue;
                        }
                        string url = reader.GetString(1);
                        bool favorite = false;
                        switch (_inclusions)
                        {
                            case 0: // bookmark and history
                                favorite = reader.GetInt32(3) == 1;
                                break;
                            case 1: // bookmarks only
                                favorite = true;
                                break;
                            case 2: // history only
                                favorite = false;
                                break;
                        }
                        if (title.ToLower(CultureInfo.InvariantCulture).Contains(query.Search.ToLower(CultureInfo.InvariantCulture)) ||
                            url.ToLower(CultureInfo.InvariantCulture).Contains(query.Search.ToLower(CultureInfo.InvariantCulture)))
                        {
                            results.Add(new Result
                            {
                                Title = title,
                                SubTitle = (favorite ? "Favorite: " : "History: ") + url,
                                IcoPath = _iconPath,
                                Action = _ =>
                                {
                                    if (!Wox.Infrastructure.Helper.OpenCommandInShell(DefaultBrowserInfo.Path, DefaultBrowserInfo.ArgumentsPattern, url))
                                    {
                                        Log.Error($"Could not open {url}", GetType());
                                        return false;
                                    }
                                    // Add delay to allow Firefox to launch and create its window
                                    Thread.Sleep(100);

                                    // Find the latest Firefox process
                                    var firefoxProcesses = Process.GetProcessesByName("firefox");
                                    if (firefoxProcesses.Any())
                                    {
                                        var firefoxProcess = firefoxProcesses
                                            .OrderByDescending(p => p.StartTime)
                                            .FirstOrDefault();

                                        if (firefoxProcess != null && firefoxProcess.MainWindowHandle != IntPtr.Zero)
                                        {
                                            SetForegroundWindow(firefoxProcess.MainWindowHandle);
                                        }
                                    }
                                    return true;
                                },
                            });
                        }
                    }
                }

            }

            return results;
        }

        public void Init(PluginInitContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _context.API.ThemeChanged += OnThemeChanged;
            UpdateIconPath(_context.API.GetCurrentTheme());
            _profiles = System.IO.Directory.GetDirectories(_path).Length;
        }

        public string GetTranslatedPluginTitle()
        {
            return Properties.Resources.plugin_name;
        }

        public string GetTranslatedPluginDescription()
        {
            return Properties.Resources.plugin_description;
        }

        private void OnThemeChanged(Theme oldtheme, Theme newTheme)
        {
            UpdateIconPath(newTheme);
        }

        private void UpdateIconPath(Theme theme)
        {
            if (theme == Theme.Light || theme == Theme.HighContrastWhite)
            {
                _iconPath = "Images/FirefoxSearch.light.png";
            }
            else
            {
                _iconPath = "Images/FirefoxSearch.dark.png";
            }
        }

        public Control CreateSettingPanel()
        {
            throw new NotImplementedException();
        }

        public void UpdateSettings(PowerLauncherPluginSettings settings)
        {
            if (settings != null && settings.AdditionalOptions != null)
            {
                _inclusions = settings.AdditionalOptions.FirstOrDefault(x => x.Key == "Inclusions")?.ComboBoxValue ?? 0;
                _maxResults = (int)(settings.AdditionalOptions.FirstOrDefault(x => x.Key == "MaxResults")?.NumberValue ?? 40);
            }
        }

        public void ReloadData()
        {
            if (_context is null)
            {
                return;
            }

            UpdateIconPath(_context.API.GetCurrentTheme());
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed && disposing)
            {
                if (_context != null && _context.API != null)
                {
                    _context.API.ThemeChanged -= OnThemeChanged;
                }

                _disposed = true;
            }
        }
    }
}
