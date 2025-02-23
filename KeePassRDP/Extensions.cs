﻿/*
 *  Copyright (C) 2018 - 2023 iSnackyCracky, NETertainer
 *
 *  This file is part of KeePassRDP.
 *
 *  KeePassRDP is free software: you can redistribute it and/or modify
 *  it under the terms of the GNU General Public License as published by
 *  the Free Software Foundation, either version 3 of the License, or
 *  (at your option) any later version.
 *
 *  KeePassRDP is distributed in the hope that it will be useful,
 *  but WITHOUT ANY WARRANTY; without even the implied warranty of
 *  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 *  GNU General Public License for more details.
 *
 *  You should have received a copy of the GNU General Public License
 *  along with KeePassRDP.  If not, see <http://www.gnu.org/licenses/>.
 *
 */

using KeePass.App.Configuration;
using KeePass.Forms;
using KeePass.Util.Spr;
using KeePassLib;
using KeePassLib.Security;
using KeePassRDP.Utils;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace KeePassRDP.Extensions
{
    /// <summary>
    /// Extension methods for the <see cref="PwEntry"/> class.
    /// </summary>
    internal static class PwEntryExtensions
    {
        /// <summary>
        /// Retrieves the <see cref="KprEntrySettings"/> of a given <see cref="PwEntry"/>.
        /// </summary>
        /// <param name="pe"><see cref="PwEntry"/> of interest.</param>
        /// <param name="readOnly">Do not touch contents of <see cref="PwEntry"/> <paramref name="pe"/>.</param>
        /// <returns><see cref="KprEntrySettings"/>, <see langword="null"/></returns>
        public static KprEntrySettings GetKprSettings(this PwEntry pe, bool readOnly = false)
        {
            var entrySettingsString = string.Empty;
            var entrySettings = pe.Binaries.Get(Util.KprEntrySettingsField);
            if (entrySettings != null && entrySettings.Length > 0)
                try
                {
                    entrySettingsString = Encoding.UTF8.GetString(entrySettings.ReadData());
                }
                catch (DecoderFallbackException)
                {
                }
            if (string.IsNullOrEmpty(entrySettingsString))
            {
                // Try to migrate from old storage location. Code should be removed after some time.
                if (pe.Strings.Exists(Util.KprEntrySettingsField))
                {
                    var oldEntrySettings = pe.Strings.ReadSafe(Util.KprEntrySettingsField);
                    /* For now delete old settings only when editing/saving entry.
                     * pe.Strings.Remove(Util.KprEntrySettingsField);
                     */
                    if (!string.IsNullOrEmpty(oldEntrySettings))
                    {
                        entrySettingsString = oldEntrySettings;
                        // Save settings to new location.
                        if (!readOnly)
                        {
                            pe.Binaries.Set(Util.KprEntrySettingsField, new ProtectedBinary(false, Encoding.UTF8.GetBytes(entrySettingsString)));
                            pe.Touch(true, false);
                        }
                    }
                    else
                        return null;
                }
                else
                    return null;
            }
            var kprEntrySettings = JsonConvert.DeserializeObject<KprEntrySettings>(entrySettingsString, Util.JsonSerializerSettings);
            kprEntrySettings.IsReadOnly = readOnly;
            return kprEntrySettings;
        }

        /// <summary>
        /// Toggles the "rdpignore-flag" of a given <see cref="PwEntry"/>.
        /// </summary>
        /// <param name="pe"><see cref="PwEntry"/> of interest.</param>
        public static void ToggleKprIgnored(this PwEntry pe)
        {
            using (var entrySettings = pe.GetKprSettings() ?? new KprEntrySettings())
            {
                // Does a CustomField "rdpignore" exist?
                if (pe.Strings.Exists(Util.KprCpIgnoreField))
                {
                    // Is the CustomField value set to "false"?
                    entrySettings.Ignore = string.Equals(pe.Strings.ReadSafe(Util.KprCpIgnoreField), bool.FalseString, StringComparison.OrdinalIgnoreCase);
                    pe.Strings.Remove(Util.KprCpIgnoreField);
                }
                // Othwerwise toggle KprEntrySettings.Ignore as the entry has no "rdpignore-flags" set.
                // Todo: Migrate "rdpignore-flag"?
                else
                    entrySettings.Ignore = !entrySettings.Ignore;

                entrySettings.SaveEntrySettings(pe);
            }
        }

        /// <summary>
        /// Uses <see cref="SprEngine"/> to resolve field references of <see cref="PwEntry"/>.
        /// </summary>
        /// <param name="pe"><see cref="PwEntry"/> of interest.</param>
        /// <param name="ctx"><see cref="SprContext"/> to use.</param>
        /// <returns><see cref="PwEntry"/></returns>
        public static PwEntry GetResolvedReferencesEntry(this PwEntry pe, SprContext ctx)
        {
            var retPwEntry = new PwEntry(false, false)
            {
                Uuid = pe.Uuid
            };

            var chars = pe.Strings.GetSafe(PwDefs.UserNameField).ReadChars();
            var entryUsername = new string(chars);
            var username = SprEngine.Compile(entryUsername, ctx);

            if (username.StartsWith(@".\", StringComparison.OrdinalIgnoreCase))
                username = Environment.GetEnvironmentVariable("COMPUTERNAME") + username.Substring(1);

            if (username != entryUsername)
            {
                MemoryUtil.SecureZeroMemory(entryUsername);
                MemoryUtil.SecureZeroMemory(chars);
                var bytes = Encoding.UTF8.GetBytes(username);
                retPwEntry.Strings.Set(PwDefs.UserNameField, new ProtectedString(true, bytes));
                MemoryUtil.SecureZeroMemory(bytes);
            }
            else
            {
                var bytes = Encoding.UTF8.GetBytes(username);
                MemoryUtil.SecureZeroMemory(entryUsername);
                MemoryUtil.SecureZeroMemory(chars);
                retPwEntry.Strings.Set(PwDefs.UserNameField, new ProtectedString(true, bytes));
                MemoryUtil.SecureZeroMemory(bytes);
            }

            chars = pe.Strings.GetSafe(PwDefs.PasswordField).ReadChars();
            var entryPassword = new string(chars);
            var password = SprEngine.Compile(entryPassword, ctx);

            if (password != entryPassword)
            {
                MemoryUtil.SecureZeroMemory(entryPassword);
                MemoryUtil.SecureZeroMemory(chars);
                var bytes = Encoding.UTF8.GetBytes(password);
                retPwEntry.Strings.Set(PwDefs.PasswordField, new ProtectedString(true, bytes));
                MemoryUtil.SecureZeroMemory(bytes);
            }
            else
            {
                var bytes = Encoding.UTF8.GetBytes(password);
                MemoryUtil.SecureZeroMemory(entryPassword);
                MemoryUtil.SecureZeroMemory(chars);
                retPwEntry.Strings.Set(PwDefs.PasswordField, new ProtectedString(true, bytes));
                MemoryUtil.SecureZeroMemory(bytes);
            }

            using (var entrySettings = pe.GetKprSettings(true) ?? KprEntrySettings.Empty)
                if (entrySettings.ForceLocalUser)
                    retPwEntry.Strings.Set(PwDefs.UserNameField, retPwEntry.Strings.GetSafe(PwDefs.UserNameField).ForceLocalUser());

            return retPwEntry;
        }
    }

    /// <summary>
    /// Extension methods for the <see cref="PwGroup"/> class.
    /// </summary>
    internal static class PwGroupExtensions
    {
        /// <summary>
        /// Filters <see cref="PwGroup"/> for matching entries.
        /// </summary>
        /// <param name="pwg"><see cref="PwGroup"/> to search in.</param>
        /// <param name="regex"><see cref="Regex"/> to filter for.</param>
        /// <returns><see cref="IEnumerable{PwEntry}"/></returns>
        internal static IEnumerable<PwEntry> GetRdpAccountEntries(this PwGroup pwg, Regex regex, PwDatabase database = null)
        {
            return pwg.Entries.Where(pe =>
            {
                if (Util.IsEntryIgnored(pe))
                    return false;

                var match = pe.Strings.ReadSafe(PwDefs.TitleField);

                if (database != null)
                    match = SprEngine.Compile(match, new SprContext(pe, database, SprCompileFlags.Deref)
                    {
                        ForcePlainTextPasswords = false
                    });

                return regex.IsMatch(match);
            });
        }
    }

    /// <summary>
    /// Extension methods for the <see cref="KprEntrySettings"/> class.
    /// </summary>
    internal static class KprEntrySettingsExtensions
    {
        /// <summary>
        /// Save <see cref="KprEntrySettings"/> to <see cref="PwEntry"/>.
        /// </summary>
        /// <param name="kprEntrySettings"><see cref="KprEntrySettings"/> to save.</param>
        /// <param name="pe"><see cref="PwEntry"/> of interest.</param>
        public static void SaveEntrySettings(this KprEntrySettings kprEntrySettings, PwEntry pe)
        {
            if (kprEntrySettings.IsReadOnly)
                return;

            string entrySettingsJson = kprEntrySettings;

            if (string.IsNullOrEmpty(entrySettingsJson))
                pe.Binaries.Remove(Util.KprEntrySettingsField);
            else
                pe.Binaries.Set(Util.KprEntrySettingsField, new ProtectedBinary(false, Encoding.UTF8.GetBytes(entrySettingsJson)));

            if (pe.Strings.Exists(Util.KprEntrySettingsField))
                pe.Strings.Remove(Util.KprEntrySettingsField);

            pe.Touch(true, false);
        }

        /// <summary>
        /// Save <see cref="KprEntrySettings"/> to <see cref="PwEntryForm"/>.
        /// </summary>
        /// <param name="kprEntrySettings"><see cref="KprEntrySettings"/> to save.</param>
        /// <param name="pwEntryForm"><see cref="PwEntryForm"/> used.</param>
        public static void SaveEntrySettings(this KprEntrySettings kprEntrySettings, PwEntryForm pwEntryForm)
        {
            if (kprEntrySettings.IsReadOnly)
                return;

            string entrySettingsJson = kprEntrySettings;

            if (string.IsNullOrEmpty(entrySettingsJson))
                pwEntryForm.EntryBinaries.Remove(Util.KprEntrySettingsField);
            else
                pwEntryForm.EntryBinaries.Set(Util.KprEntrySettingsField, new ProtectedBinary(false, Encoding.UTF8.GetBytes(entrySettingsJson)));

            if (pwEntryForm.EntryStrings.Exists(Util.KprEntrySettingsField))
                pwEntryForm.EntryStrings.Remove(Util.KprEntrySettingsField);
        }
    }

    /// <summary>
    /// Extension methods for the <see cref="AceCustomConfig"/> class.
    /// </summary>
    internal static class AceCustomConfigExtensions
    {
        /// <summary>
        /// Remove key <paramref name="strID"/> from <see cref="AceCustomConfig"/> if <paramref name="value"/> is equal to <paramref name="defaultValue"/>.
        /// </summary>
        /// <typeparam name="T">
        /// Type of <paramref name="value"/>.
        /// </typeparam>
        /// <param name="config"></param>
        /// <param name="strID"></param>
        /// <param name="value"></param>
        /// <param name="defaultValue"></param>
        /// <returns><see langword="true"/> if <paramref name="value"/> equals <paramref name="defaultValue"/>, <see langword="false"/> otherwise.</returns>
        public static bool RemoveIfDefault<T>(this AceCustomConfig config, string strID, T value, T defaultValue)
        {
            if (defaultValue.Equals(value))
            {
                if (config.GetString(strID) != null)
                    config.SetString(strID, null);
                return true;
            }
            return false;
        }
    }

    /*internal static class TextBoxExtensions
    {
        [DllImport("User32.dll", EntryPoint = "SendMessageW", CharSet = CharSet.Unicode)]
        private static extern int SendMessage(IntPtr hWnd, int msg, int wParam, [MarshalAs(UnmanagedType.LPWStr)] string lParam);

        private const int EM_SETCUEBANNER = 0x1501;

        public static void SetPlaceholder(this TextBox box, string placeholder)
        {
            SendMessage(box.Handle, EM_SETCUEBANNER, 0, placeholder);
        }
    }*/

    /// <summary>
    /// Extension methods for the <see cref="ISet{T}"/> interface.
    /// </summary>
    internal static class ISetExtensions
    {
        /// <summary>
        /// Returns read-only wrapper for the set.
        /// </summary>
        public static ReadOnlySet<T> AsReadOnly<T>(this ISet<T> set)
        {
            return new ReadOnlySet<T>(set);
        }
    }

    /// <summary>
    /// Extension methods for the <see cref="IList{T}"/> interface.
    /// </summary>
    internal static class IListExtensions
    {
        /// <summary>
        /// Returns read-only wrapper for the collection.
        /// </summary>
        public static ReadOnlyCollection<T> AsReadOnly<T>(this IList<T> list)
        {
            return new ReadOnlyCollection<T>(list);
        }
    }
}