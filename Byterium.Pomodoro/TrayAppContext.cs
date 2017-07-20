using LuxaforSharp;
using System;
using System.Drawing;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Byterium.Pomodoro
{
    public class TrayAppContext : ApplicationContext, IDisposable
    {
        private readonly NotifyIcon _notifyIcon;

        private readonly DeviceList _luxaforDevices = new DeviceList();
        private IDevice _luxaforDevice;

        public TrayAppContext()
        {
            var startPomodoroMenuItem = new MenuItem("&Start pomodoro");
            
            var pomodoroState = Observable
                .Switch(
                    Observable
                        .FromEventPattern<EventHandler, EventArgs>(
                            handler => startPomodoroMenuItem.Click += handler,
                            handler => startPomodoroMenuItem.Click -= handler
                        )
                        .Select(evt => StartPomodoro())
                );

            pomodoroState.Subscribe(async state => await OnPomodoroStateChanged(state));
            
            var contextMenu = new ContextMenu(new[]
            {
                startPomodoroMenuItem,
                new MenuItem("&Reconnect", async (sender, evt) => await ConnectLuxafor())
            });
            
            _notifyIcon = new NotifyIcon
            {
                Icon = new Icon(SystemIcons.Shield, 40, 40),
                Visible = true,
                ContextMenu = contextMenu
            };

            ConnectLuxafor();
        }

        private async Task ConnectLuxafor()
        {
            if (_luxaforDevice != null)
            {
                _luxaforDevice.Dispose();
            }

            _luxaforDevices.Scan();
            _luxaforDevice = _luxaforDevices.FirstOrDefault();

            if (_luxaforDevice != null)
            {
                await _luxaforDevice?.SetColor(LedTarget.All, new LuxaforSharp.Color(0, 0, 0));
                await _luxaforDevice.Wave(WaveType.OverlappingShort, new LuxaforSharp.Color(255, 255, 255), 5, 2);
            }
        }

        private IObservable<PomodoroState> StartPomodoro()
        {
            return Observable
                .Return(PomodoroState.Pomodoro)
                .Concat(
                    Observable
                        .Timer(TimeSpan.FromSeconds(5))
                        .SelectMany(t => StartShortBreak())
                );
        }

        private IObservable<PomodoroState> StartShortBreak()
        {
            return Observable
                .Return(PomodoroState.ShortBreak)
                .Concat(
                    Observable
                        .Timer(TimeSpan.FromSeconds(2))
                        .Select(t => PomodoroState.ShortBreakEnded)
                );
        }

        private async Task OnPomodoroStateChanged(PomodoroState newState)
        {
            switch (newState)
            {
                case PomodoroState.Pomodoro:
                    await _luxaforDevice?.SetColor(LedTarget.All, new LuxaforSharp.Color(255, 0, 0));
                    break;
                case PomodoroState.ShortBreak:
                    await _luxaforDevice?.SetColor(LedTarget.All, new LuxaforSharp.Color(0, 255, 0));
                    break;
                case PomodoroState.ShortBreakEnded:
                    await _luxaforDevice?.SetColor(LedTarget.All, new LuxaforSharp.Color(0, 0, 0));
                    await _luxaforDevice?.Blink(LedTarget.All, new LuxaforSharp.Color(0, 0, 255), 5, 3);
                    break;
            }
        }
    }
}
