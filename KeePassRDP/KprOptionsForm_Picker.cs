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

using KeePassRDP.Utils;
using System;
using System.ComponentModel;
using System.Linq;
using System.Windows.Forms;

namespace KeePassRDP
{
    public partial class KprOptionsForm
    {
        private void txtCredPickerCustomGroup_KeyDown(object sender, KeyEventArgs e)
        {
            txtCredPickerCustomGroup_Enter(sender, EventArgs.Empty);
            var textBox = sender as TextBox;
            if (string.IsNullOrEmpty(textBox.Text))
                return;
            if (!Util.ClickButtonOnEnter(null, e))
                Util.ResetTextOnEscape(textBox, e);
        }

        private void txtCredPickerCustomGroup_ShowToolTip(object sender, EventArgs e)
        {
            var timer = sender as Timer;
            timer.Enabled = false;

            var control = timer.Tag as Control;
            if (!string.IsNullOrEmpty(ttTrigger.GetToolTip(control)))
                return;

            var point = control.PointToClient(Cursor.Position);
            var size = _txtCredPickerCustomGroupCursorSize.Value;

            if (!size.IsEmpty)
                point.Y += size.Height / 2;

            point.X += 2;
            point.Y += 1;

            ttTrigger.Show(
                KprResourceManager.Instance["Define a custom group name that triggers the credential picker.\r\nDefaults to \"RDP\" if unset."],
                control,
                point,
                ttTrigger.AutoPopDelay);
        }

        private void txtCredPickerCustomGroup_MouseEnter(object sender, EventArgs e)
        {
            _txtCredPickerCustomGroupTooltipTimer.Tag = sender;
            _txtCredPickerCustomGroupTooltipTimer.Tick += txtCredPickerCustomGroup_ShowToolTip;
            if (_txtCredPickerCustomGroupTooltipTimer.Enabled)
                _txtCredPickerCustomGroupTooltipTimer.Enabled = false;
            _txtCredPickerCustomGroupTooltipTimer.Enabled = !_lastTooltipMousePosition.HasValue;
        }

        private void txtCredPickerCustomGroup_MouseLeave(object sender, EventArgs e)
        {
            _txtCredPickerCustomGroupTooltipTimer.Tick -= txtCredPickerCustomGroup_ShowToolTip;
            if (_txtCredPickerCustomGroupTooltipTimer.Enabled)
                _txtCredPickerCustomGroupTooltipTimer.Enabled = false;
            ttTrigger.Hide(sender as Control);
            _lastTooltipMousePosition = null;
        }

        private void txtCredPickerCustomGroup_MouseMove(object sender, MouseEventArgs e)
        {
            if (_lastTooltipMousePosition.HasValue && _lastTooltipMousePosition.Value == e.Location)
                return;
            _lastTooltipMousePosition = e.Location;
            if (_txtCredPickerCustomGroupTooltipTimer.Enabled)
                _txtCredPickerCustomGroupTooltipTimer.Enabled = false;
            _txtCredPickerCustomGroupTooltipTimer.Enabled = true;
        }

        private void txtCredPickerCustomGroup_Enter(object sender, EventArgs e)
        {
            if (_txtCredPickerCustomGroupTooltipTimer.Enabled)
                _txtCredPickerCustomGroupTooltipTimer.Enabled = false;
            ttTrigger.Hide(sender as Control);
        }

        private void txtCredPickerCustomGroup_Leave(object sender, EventArgs e)
        {
            if (_txtCredPickerCustomGroupTooltipTimer.Enabled)
                _txtCredPickerCustomGroupTooltipTimer.Enabled = false;
            ttTrigger.Hide(sender as Control);
            _lastTooltipMousePosition = null;
        }

        private void txtRegExPre_KeyDown(object sender, KeyEventArgs e)
        {
            var textBox = sender as TextBox;
            if (string.IsNullOrEmpty(textBox.Text))
                return;
            if (!Util.ClickButtonOnEnter(cmdRegExPreAdd, e))
                Util.ResetTextOnEscape(textBox, e);
        }

        private void txtRegExPost_KeyDown(object sender, KeyEventArgs e)
        {
            var textBox = sender as TextBox;
            if (string.IsNullOrEmpty(textBox.Text))
                return;
            if (!Util.ClickButtonOnEnter(cmdRegExPostAdd, e))
                Util.ResetTextOnEscape(textBox, e);
        }

        private void txtRegExPre_TextChanged(object sender, EventArgs e)
        {
            cmdRegExPreAdd.Enabled = !string.IsNullOrEmpty(txtRegExPre.Text);
        }

        private void txtRegExPost_TextChanged(object sender, EventArgs e)
        {
            cmdRegExPostAdd.Enabled = !string.IsNullOrEmpty(txtRegExPost.Text);
        }

        private void lstRegExPre_SelectedIndexChanged(object sender, EventArgs e)
        {
            cmdRegExPreRemove.Enabled = lstRegExPre.SelectedItems.Count > 0;
        }

        private void lstRegExPost_SelectedIndexChanged(object sender, EventArgs e)
        {
            cmdRegExPostRemove.Enabled = lstRegExPost.SelectedItems.Count > 0;
        }

        private void cmdRegExPreAdd_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(txtRegExPre.Text))
                return;

            (lstRegExPre.DataSource as BindingList<string>).Add(txtRegExPre.Text);
            txtRegExPre.ResetText();

            ResetActiveControl(sender as Control);
        }

        private void cmdRegExPreRemove_Click(object sender, EventArgs e)
        {
            var list = lstRegExPre.DataSource as BindingList<string>;

            lstRegExPre.BeginUpdate();
            foreach(var i in lstRegExPre.SelectedIndices.Cast<int>().OrderByDescending(x => x))
                list.RemoveAt(i);
            lstRegExPre.EndUpdate();

            ResetActiveControl(sender as Control);
        }

        private void cmdRegExPreReset_Click(object sender, EventArgs e)
        {
            var list = lstRegExPre.DataSource as BindingList<string>;

            lstRegExPre.BeginUpdate();
            list.Clear();
            foreach (var regex in Util.DefaultCredPickRegExPre.Split('|'))
                list.Add(regex);
            lstRegExPre.EndUpdate();

            ResetActiveControl(sender as Control);
        }

        private void cmdRegExPostAdd_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(txtRegExPost.Text))
                return;

            (lstRegExPost.DataSource as BindingList<string>).Add(txtRegExPost.Text);
            txtRegExPost.Text = string.Empty;

            ResetActiveControl(sender as Control);
        }

        private void cmdRegExPostRemove_Click(object sender, EventArgs e)
        {
            var list = lstRegExPost.DataSource as BindingList<string>;

            lstRegExPost.BeginUpdate();
            foreach (var i in lstRegExPost.SelectedIndices.Cast<int>().OrderByDescending(x => x))
                list.RemoveAt(i);
            lstRegExPost.EndUpdate();

            ResetActiveControl(sender as Control);
        }

        private void cmdRegExPostReset_Click(object sender, EventArgs e)
        {
            var list = lstRegExPost.DataSource as BindingList<string>;

            lstRegExPost.BeginUpdate();
            list.Clear();
            foreach (var regex in Util.DefaultCredPickRegExPost.Split('|'))
                list.Add(regex);
            lstRegExPost.EndUpdate();

            ResetActiveControl(sender as Control);
        }
    }
}