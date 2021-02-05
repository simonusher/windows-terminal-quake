using Serilog;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows.Automation;

namespace WindowsTerminalQuake.Native
{
	public class FocusTracker
	{
		public static event EventHandler OnFocusLost = delegate { };

		private static bool _isRunning;

		public static void FocusGained(Process process)
		{
			Log.Information("Focus gained");

			if (_isRunning) return;

			SubscribeToFocusChange(process);

			//_isRunning = true;

			//Task.Run(async () =>
			//{
			//	while (_isRunning)
			//	{
			//		await Task.Delay(TimeSpan.FromMilliseconds(100));

			//		var main = process.MainWindowHandle;
			//		if (main != IntPtr.Zero)
			//		{
			//			var fg = User32.GetForegroundWindow();
			//			if (process.MainWindowHandle != fg)
			//			{
			//				Log.Information("Focus lost");
			//				OnFocusLost(null, null);
			//				_isRunning = false;
			//				break;
			//			}
			//		}
			//	}
			//});
		}



		public static void SubscribeToFocusChange(Process p)
		{
			AutomationFocusChangedEventHandler focusHandler = new AutomationFocusChangedEventHandler((sender, e) => OnFocusChanged(sender, e, p));
			Automation.AddAutomationFocusChangedEventHandler(focusHandler);
		}

		private static void OnFocusChanged(object sender, AutomationFocusChangedEventArgs e, Process p)
		{
			AutomationElement focusedElement = sender as AutomationElement;
			if (focusedElement != null)
			{

				int main = p.MainWindowHandle.ToInt32();
				int ble = focusedElement.Current.NativeWindowHandle;

				Log.Information($"Current focused handle: {ble}");
				Log.Information($"Process handle: {main}");

				if (main != ble)
				{
					Automation.RemoveAllEventHandlers();
					Log.Information("Focus lost");
					OnFocusLost(null, null);
				}
			}
			else
			{
				Log.Information("Focus null");
			}
		}
	}
}