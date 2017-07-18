// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace Services
{
	/// <summary>
	/// Tracks a form's window state and position and enables an application to 
	/// restore it upon subsequent invocations. Provides enhanced support for maximizing 
	/// across multiple monitors. Enables locating subforms on the same monitor as a main form.
	/// </summary>
	public class WindowRestorerService
	{
		private Form form;
		private bool windowInitialized;

		private Rectangle saveRestoreBounds;
		private bool pseudoMaxActive;

		/// <summary>
		/// Initializes a new instance of the <see cref="WindowRestorer"/> class 
		/// using defaults.
		/// </summary>
		/// <param name="form">The target form.</param>
		public WindowRestorerService(Form form)
		{
			this.form = form;

			WindowPosition = Rectangle.Empty;
			WindowState = FormWindowState.Normal;
			Restore();
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="WindowRestorer"/> class.
		/// </summary>
		/// <param name="form">The target form.</param>
		/// <param name="persistedWindowPosition">The persisted window position.</param>
		/// <param name="persistedWindowState">The persisted window state.</param>
		public WindowRestorerService(Form form,
			Rectangle persistedWindowPosition, FormWindowState persistedWindowState)
		{
			this.form = form;

			WindowPosition = persistedWindowPosition;
			WindowState = persistedWindowState;
			Restore();
		}

		/// <summary>
		/// Tracks a form's window state and position.
		/// </summary>
		/// <remarks>
		/// This remembers changes as they occur.
		/// (If you were instead to only record the state and position on closing the form,
		/// it would not work if the application was minimized or maximized.)
		/// At closing, simply transfer the <see cref="WindowPosition"/>
		/// and <see cref="WindowState"/> to your persistent setting store.
		/// </remarks>
		public void TrackWindow()
		{
			// Don't record the window setup, otherwise we lose the persistent values!
			if (!windowInitialized)
				return;

			if (form.WindowState == FormWindowState.Normal)
				WindowPosition = form.DesktopBounds;

			if (form.WindowState != FormWindowState.Minimized)
				WindowState = form.WindowState;
		}

		private bool IsVisibleOnAnyScreen(Rectangle rect)
		{
			foreach (Screen screen in Screen.AllScreens)
				if (screen.WorkingArea.IntersectsWith(rect))
					return true;
			return false;
		}

		/// <summary>
		/// Restore the window.
		/// </summary>
		private void Restore()
		{
			windowInitialized = false;

			// This is the default.
			form.WindowState = FormWindowState.Normal;
			form.StartPosition = FormStartPosition.WindowsDefaultBounds;

			// Check if the saved bounds are nonzero and visible on any screen.
			if (WindowPosition != Rectangle.Empty && IsVisibleOnAnyScreen(WindowPosition))
			{
				// first set the bounds.
				form.StartPosition = FormStartPosition.Manual;
				form.DesktopBounds = WindowPosition;

				// Afterwards set the window state to the saved value (which could be Maximized).
				form.WindowState = WindowState;
			}
			else
			{
				// This resets the upper left corner of the window to windows standards.
				form.StartPosition = FormStartPosition.WindowsDefaultLocation;

				// We can still apply the saved size, if any.
				if (WindowPosition != Rectangle.Empty)
					form.Size = WindowPosition.Size;
			}

			// Signal event handlers OK to process.
			windowInitialized = true;
		}

		/// <summary>
		/// Sets the location of a sub-window relative to its base form
		/// and optionally a parent control on that base form,
		/// allowing for multiple monitors.
		/// </summary>
		/// <param name="targetForm">The target form.</param>
		/// <param name="parentRelativeLocation">The relative offset from the base form.</param>
		/// <param name="xOffset">An additional fixed x offset.</param>
		/// <param name="yOffset">An additional fixed y offset.</param>
		public static void SetSubWindowRelativeLocation(Form targetForm,
			Point parentRelativeLocation, int xOffset, int yOffset)
		{
			if (targetForm.Owner == null)
				return;

			targetForm.Location = new Point(
				 targetForm.Owner.Location.X + parentRelativeLocation.X + xOffset,
				 targetForm.Owner.Location.Y + parentRelativeLocation.Y + yOffset);
			targetForm.StartPosition = FormStartPosition.Manual;
		}

		/// <summary>
		/// Sets the location of a sub-window relative to its base form
		/// allowing for multiple monitors.
		/// </summary>
		/// <seealso cref="SetSubWindowRelativeLocation(Form,Point,int,int)"/>
		/// <param name="targetForm">The target form.</param>
		public static void SetSubWindowRelativeLocation(Form targetForm)
		{
			SetSubWindowRelativeLocation(targetForm, new Point(0, 0), 0, 0);
		}

		// From http://www.c-sharpcorner.com/Forums/ShowMessages.aspx?ThreadID=52
		//[DllImport("user32.dll")]
		//private static extern int SendMessage(IntPtr hWnd, Int32 wMsg, bool wParam, Int32 lParam);
		//private const int WM_SETREDRAW = 11; 

		/// <summary>
		/// Maximizes a form over multiple monitors.
		/// </summary>
		/// <remarks>
		/// Allows for the main monitor being the leftmost
		/// (in which case its top-left corner is the top-left corner of the spread)
		/// or not the leftmost (in which case the top-left corner is negative).
		/// Assumes that monitors are laid out in a single horizontal line.
		/// Allows for differing monitor height, using the smallest so that
		/// the entire form window is visible.
		/// </remarks>
		/// <returns>true if form was resized to multiple monitors</returns>
		public bool MultipleMonitorMaximize()
		{
			bool retVal = false;
			if (form.WindowState == FormWindowState.Maximized
				 && Control.ModifierKeys == Keys.Control
				 && Screen.AllScreens.Length > 1)
			{
				// This reduces flicker a bit but also
				// leaves screen artifacts behind sometimes so not using it.
				//SendMessage(form.Handle, WM_SETREDRAW, false, 0);

				if (pseudoMaxActive)
				{
					// Currently pseudo-maximized; restore down to normal.
					form.WindowState = FormWindowState.Normal;
					form.DesktopBounds = saveRestoreBounds;
					pseudoMaxActive = false;
				}
				else
				{
					// Pseudo-maximize to multiple monitors.
					saveRestoreBounds = form.RestoreBounds;
					pseudoMaxActive = true;

					Point startPoint = new Point(
						 Screen.AllScreens.Min(screen => screen.WorkingArea.Left),
						 Screen.AllScreens.Max(screen => screen.WorkingArea.Top));
					Point endPoint = new Point(
						 Screen.AllScreens.Max(screen => screen.WorkingArea.Right),
						 Screen.AllScreens.Min(screen => screen.WorkingArea.Bottom));
					Size newSize = new Size(endPoint.X - startPoint.X, endPoint.Y - startPoint.Y);

					form.WindowState = FormWindowState.Normal;
					form.Location = startPoint;
					form.Size = newSize;
				}
				//SendMessage(form.Handle, WM_SETREDRAW, true, 0);
				retVal = true;
			}
			return retVal;
		}

		/// <summary>
		/// Gets the window position.
		/// </summary>
		/// <value>The window position.</value>
		public Rectangle WindowPosition { get; private set; }

		/// <summary>
		/// Gets the window state.
		/// </summary>
		/// <value>The window state.</value>
		public FormWindowState WindowState { get; private set; }
	}
}
