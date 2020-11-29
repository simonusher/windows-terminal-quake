using Serilog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using WindowsTerminalQuake.Native;
using System.Diagnostics;
using System.Threading;
using System.Timers;

namespace WindowsTerminalQuake
{
	public class Toggler : IDisposable
	{
		private Process _process => TerminalProcess.Get();

		private readonly List<int> _registeredHotKeys = new List<int>();

		public Toggler()
		{
			// Always on top
			if (Settings.Instance.AlwaysOnTop) TopMostWindow.SetTopMost(_process);

			// Hide from taskbar
			User32.SetWindowLong(_process.MainWindowHandle, User32.GWL_EX_STYLE, (User32.GetWindowLong(_process.MainWindowHandle, User32.GWL_EX_STYLE) | User32.WS_EX_TOOLWINDOW) & ~User32.WS_EX_APPWINDOW);

			User32.Rect rect = default;
			User32.ShowWindow(_process.MainWindowHandle, NCmdShow.MAXIMIZE);

			var isOpen = false;

			// Register hotkeys
			Settings.Get(s =>
			{
				_registeredHotKeys.ForEach(hk => HotKeyManager.UnregisterHotKey(hk));
				_registeredHotKeys.Clear();

				s.Hotkeys.ForEach(hk =>
				{
					Log.Information($"Registering hot key {hk.Modifiers} + {hk.Key}");
					var reg = HotKeyManager.RegisterHotKey(hk.Key, hk.Modifiers);
					_registeredHotKeys.Add(reg);
				});
			});

			FocusTracker.OnFocusLost += (s, a) =>
			{
				if (Settings.Instance.HideOnFocusLost && isOpen)
				{
					isOpen = false;
					Toggle(false, 0);
				}
			};

			HotKeyManager.HotKeyPressed += (s, a) =>
			{
				Toggle(!isOpen, Settings.Instance.ToggleDurationMs);
				isOpen = !isOpen;
			};
		}


		public double getPercentage(Stopwatch stopwatch, int durationMs)
		{
			return Math.Min((double)stopwatch.ElapsedMilliseconds / durationMs, 1);
		}


		private void ToggleInstant(bool open)
		{
			Log.Information("INSTANT");
			var screen = GetScreenWithCursor();
			var bounds = screen.Bounds;

			var scrWidth = bounds.Width;
			var horWidthPct = (float)Settings.Instance.HorizontalScreenCoverage;

			var horWidth = (int)Math.Ceiling(scrWidth / 100f * horWidthPct);
			var x = 0;

			bounds.Height = (int)Math.Ceiling((bounds.Height / 100f) * Settings.Instance.VerticalScreenCoverage);
			bounds.Height += Settings.Instance.VerticalOffset;

			switch (Settings.Instance.HorizontalAlign)
			{
				case HorizontalAlign.Left:
					x = bounds.X;
					break;

				case HorizontalAlign.Right:
					x = bounds.X + (bounds.Width - horWidth);
					break;

				case HorizontalAlign.Center:
				default:
					x = bounds.X + (int)Math.Ceiling(scrWidth / 2f - horWidth / 2f);
					break;
			}
			if (!open)
			{

				Log.Information("Close");

				User32.ShowWindow(_process.MainWindowHandle, NCmdShow.RESTORE);
				User32.SetForegroundWindow(_process.MainWindowHandle);
				var newY = bounds.Y - bounds.Height + Settings.Instance.VerticalOffset;

				User32.MoveWindow(_process.MainWindowHandle, x, newY, horWidth, bounds.Height, true);
				// Minimize, so the last window gets focus
				User32.ShowWindow(_process.MainWindowHandle, NCmdShow.MINIMIZE);

				// Hide, so the terminal windows doesn't linger on the desktop
				User32.ShowWindow(_process.MainWindowHandle, NCmdShow.HIDE);
			}
			else
			{
				Log.Information("Open");
				FocusTracker.FocusGained(_process);

				User32.ShowWindow(_process.MainWindowHandle, NCmdShow.RESTORE);
				User32.SetForegroundWindow(_process.MainWindowHandle);
				var newY = bounds.Y + Settings.Instance.VerticalOffset;

				User32.MoveWindow(_process.MainWindowHandle, x, newY, horWidth, bounds.Height, true);
				if (Settings.Instance.VerticalScreenCoverage >= 100 && Settings.Instance.HorizontalScreenCoverage >= 100)
				{
					User32.ShowWindow(_process.MainWindowHandle, NCmdShow.MAXIMIZE);
				}
			}
		}

		public void Toggle(bool open, int durationMs)
		{
			if(durationMs == 0)
			{
				ToggleInstant(open);
				return;
			}
			var stepCount = (int)Math.Max(Math.Ceiling(durationMs / 7f), 1f);
			var stepDelayMs = durationMs / stepCount;

			var screen = GetScreenWithCursor();
			var bounds = screen.Bounds;

			var scrWidth = bounds.Width;
			var horWidthPct = (float)Settings.Instance.HorizontalScreenCoverage;

			var horWidth = (int)Math.Ceiling(scrWidth / 100f * horWidthPct);
			var x = 0;

			bounds.Height = (int)Math.Ceiling((bounds.Height / 100f) * Settings.Instance.VerticalScreenCoverage);
			bounds.Height += Settings.Instance.VerticalOffset;

			switch (Settings.Instance.HorizontalAlign)
			{
				case HorizontalAlign.Left:
					x = bounds.X;
					break;

				case HorizontalAlign.Right:
					x = bounds.X + (bounds.Width - horWidth);
					break;

				case HorizontalAlign.Center:
				default:
					x = bounds.X + (int)Math.Ceiling(scrWidth / 2f - horWidth / 2f);
					break;
			}

			// Close
			Stopwatch sw = new Stopwatch();
			var timer = new System.Timers.Timer(7);
			timer.AutoReset = true;
			if (!open)
			{
				Log.Information("Close");

				User32.ShowWindow(_process.MainWindowHandle, NCmdShow.RESTORE);
				User32.SetForegroundWindow(_process.MainWindowHandle);

				timer.Elapsed += (source, e) =>
				{
					double elapsedPercentage =  getPercentage(sw, durationMs);
					Log.Information(elapsedPercentage.ToString());

					int hiddenAmount = (int)(bounds.Height * (1 - elapsedPercentage));
					if (elapsedPercentage == 1)
					{
						hiddenAmount = 0;
					}
					var newY = bounds.Y + -bounds.Height + hiddenAmount + Settings.Instance.VerticalOffset;

					User32.MoveWindow(_process.MainWindowHandle, x, newY, horWidth, bounds.Height, true);

					if(elapsedPercentage == 1)
					{
						// Minimize, so the last window gets focus
						User32.ShowWindow(_process.MainWindowHandle, NCmdShow.MINIMIZE);

						// Hide, so the terminal windows doesn't linger on the desktop
						User32.ShowWindow(_process.MainWindowHandle, NCmdShow.HIDE);
						sw.Stop();
						Log.Information(sw.ElapsedMilliseconds.ToString());
						timer.Stop();
						timer.Dispose();
					}
				};
			}
			// Open
			else
			{
				Log.Information("Open");
				FocusTracker.FocusGained(_process);

				User32.ShowWindow(_process.MainWindowHandle, NCmdShow.RESTORE);
				User32.SetForegroundWindow(_process.MainWindowHandle);

				timer.Elapsed += (source, e) =>
				{
					double elapsedPercentage = getPercentage(sw, durationMs);
					Log.Information(elapsedPercentage.ToString());

					int shownAmount = (int)(bounds.Height * elapsedPercentage);
					if (elapsedPercentage == 1)
					{
						shownAmount = bounds.Height;
					}
					var newY = bounds.Y + -bounds.Height + shownAmount + Settings.Instance.VerticalOffset;

					User32.MoveWindow(_process.MainWindowHandle, x, newY, horWidth, bounds.Height, true);

					if (elapsedPercentage == 1)
					{
						if (Settings.Instance.VerticalScreenCoverage >= 100 && Settings.Instance.HorizontalScreenCoverage >= 100)
						{
							User32.ShowWindow(_process.MainWindowHandle, NCmdShow.MAXIMIZE);
						}
						sw.Stop();
						Log.Information(sw.ElapsedMilliseconds.ToString());
						timer.Stop();
						timer.Dispose();
					}
				};
			}
			timer.Enabled = true;
			sw.Start();
		}

		public Rectangle GetBounds(Screen screen, int stepCount, int step)
		{
			var bounds = screen.Bounds;

			var scrWidth = bounds.Width;
			var horWidthPct = (float)Settings.Instance.HorizontalScreenCoverage;

			var horWidth = (int)Math.Ceiling(scrWidth / 100f * horWidthPct);
			var x = 0;

			switch (Settings.Instance.HorizontalAlign)
			{
				case HorizontalAlign.Left:
					x = bounds.X;
					break;

				case HorizontalAlign.Right:
					x = bounds.X + (bounds.Width - horWidth);
					break;

				case HorizontalAlign.Center:
				default:
					x = bounds.X + (int)Math.Ceiling(scrWidth / 2f - horWidth / 2f);
					break;
			}

			bounds.Height = (int)Math.Ceiling((bounds.Height / 100f) * Settings.Instance.VerticalScreenCoverage);
			bounds.Height += Settings.Instance.VerticalOffset;

			return new Rectangle(
				x,
				bounds.Y + -bounds.Height + (bounds.Height / stepCount * step) + Settings.Instance.VerticalOffset,
				horWidth,
				bounds.Height
			);
		}

		public void Dispose()
		{
			ResetTerminal(_process);
		}

		private static Screen GetScreenWithCursor()
		{
			return Screen.AllScreens.FirstOrDefault(s => s.Bounds.Contains(Cursor.Position));
		}

		private static void ResetTerminal(Process process)
		{
			process.Kill();
			//var bounds = GetScreenWithCursor().Bounds;

			//// Restore taskbar icon
			//User32.SetWindowLong(process.MainWindowHandle, User32.GWL_EX_STYLE, (User32.GetWindowLong(process.MainWindowHandle, User32.GWL_EX_STYLE) | User32.WS_EX_TOOLWINDOW) & User32.WS_EX_APPWINDOW);

			//// Reset position
			//User32.MoveWindow(process.MainWindowHandle, bounds.X, bounds.Y, bounds.Width, bounds.Height, true);

			//// Restore window
			//User32.ShowWindow(process.MainWindowHandle, NCmdShow.MAXIMIZE);
		}
	}
}