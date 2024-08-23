using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

using NAudio.Midi;

namespace wpf_pedal_ui
{
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window
	{
		private enum PedalMode { Record, Play, Auto };
		private enum PedalState { None, Record, Overdub, Play, Stop, Clear };
		private readonly int[] _midiNotes = [ 40, 41, 43, 53, 55, 57, 59 ];

		private bool[] _muteTrack = [false, false, false, false];
		private int _selectedTrack = 0; //0, 1; 2; 3;
		private PedalMode _mode = PedalMode.Record;
		private PedalState _state = PedalState.None;

		private MidiIn? _inputDevice = null;
		private MidiOut? _outputPedal = null;

		private readonly Brush _red = Brushes.Red;
		private readonly Brush _orange = Brushes.Orange;
		private readonly Brush _green = Brushes.Lime;
		private readonly Brush _blue = Brushes.Blue;

		private Stopwatch _stopWatch = new(); //Slows Rec/Play pedal presses
		private Stopwatch _loopStopWatch = new();
		private DispatcherTimer _loopTimer = new();
		private double _loopLength = 0.0;

		public MainWindow()
		{
			InitializeComponent();
			Clear();
			txtOutput.Text = string.Empty;
			_loopTimer.Tick += _loopTimer_Tick;
		}

		private void _loopTimer_Tick(object? sender, EventArgs e)
		{
			double i = _loopLength;
			guiProgress.Value += i;

			if(guiProgress.Value >= guiProgress.Maximum)
			{
				guiProgress.Value = 0;
			}
		}

		private void SendNotetoPedal(int noteNumber)
		{
			if (_outputPedal != null)
			{
				NoteOnEvent note = new(0, 1, noteNumber, 127, 0);
				_outputPedal.Send(note.GetAsShortMessage());
			}
		}

		private void UndoTrack(int i)
		{
			_stopWatch.Stop();
			_stopWatch.Reset();
			Trace.WriteLine("UNDO TRACK " + i);
		}

		private void ReleaseDevices()
		{
			try
			{
				if (_inputDevice != null)
				{
					_inputDevice.MessageReceived -= MidiIn_MessageReceived;
					_inputDevice.Stop();
					_inputDevice.Dispose();
				}

				_outputPedal?.Dispose();
			}
			catch (Exception e)
			{
				txtOutput.Text = e.Message;
			}
		}

		private void ScanForDevices()
		{
			ReleaseDevices();

			InputDeviceList.Items.Clear();
			if (MidiIn.NumberOfDevices > 0)
			{
				for (int i = 0; i < MidiIn.NumberOfDevices; i++)
				{
					InputDeviceList.Items.Add(MidiIn.DeviceInfo(i).ProductName);
				}
			}

			OutputDeviceList.Items.Clear();
			if (MidiOut.NumberOfDevices > 0)
			{
				for (int i = 0; i < MidiOut.NumberOfDevices; i++)
				{
					OutputDeviceList.Items.Add(MidiOut.DeviceInfo(i).ProductName);
				}
			}
		}

		private void UpdateUI()
		{
			switch (_mode)
			{
				case PedalMode.Record:
					ledTrackOne.Background = _selectedTrack == 0 ? _red : Brushes.Transparent;
					ledTrackTwo.Background = _selectedTrack == 1 ? _red : Brushes.Transparent;
					ledTrackThree.Background = _selectedTrack == 2 ? _red : Brushes.Transparent;
					ledTrackFour.Background = _selectedTrack == 3 ? _red : Brushes.Transparent;
					break;

				case PedalMode.Play:
					ledTrackOne.Background = !_muteTrack[0] ? _green : Brushes.Transparent;
					ledTrackTwo.Background = !_muteTrack[1] ? _green : Brushes.Transparent;
					ledTrackThree.Background = !_muteTrack[2] ? _green : Brushes.Transparent;
					ledTrackFour.Background = !_muteTrack[3] ? _green : Brushes.Transparent;
					break;

				default:
					ledTrackOne.Background = Brushes.Transparent;
					ledTrackTwo.Background = Brushes.Transparent;
					ledTrackThree.Background = Brushes.Transparent;
					ledTrackFour.Background = Brushes.Transparent;
					break;
			}

			if (_state == PedalState.Clear)
			{
				ledPlayRec.Background = Brushes.White;
				ledStop.Background = Brushes.White;
			}

			//Update state led
			ledPlayRec.Background = Brushes.Transparent; //none (default)
			if (_state == PedalState.Record) ledPlayRec.Background = _red; //recMode
			if (_state == PedalState.Overdub) ledPlayRec.Background = _orange; //overMode
			if (_state == PedalState.Play) ledPlayRec.Background = _green; //playMode
			ledStop.Background = (_state == PedalState.Stop) ? _blue : Brushes.Transparent; //stopMode

			switch(_state)
			{
				case PedalState.Record:
					guiProgress.Foreground = _red;
					break;

				case PedalState.Overdub:
					guiProgress.Foreground = _orange;
					break;

				case PedalState.Play:
					guiProgress.Foreground = _green;
					break;

				case PedalState.Stop:
					guiProgress.Foreground = _green;
					break;

				default:
					guiProgress.Foreground = _red;
					break;
			}
		}

		private void ChangeMode(PedalMode newMode = PedalMode.Auto)
		{
			if (newMode == PedalMode.Auto)
				_mode = _mode == PedalMode.Record ? PedalMode.Play : PedalMode.Record;
			else
				_mode = newMode;

			UpdateUI();
		}

		private void Stop()
		{
			if (_state == PedalState.None)
				return;

			_state = PedalState.Stop;
			_loopTimer.Stop();

			UpdateUI();
		}

		private void ChangeState()
		{
			switch(_state)
			{
				case PedalState.None:
					_state = PedalState.Record;
					//Reset loopStopWatch and Start
					_loopStopWatch.Reset();
					_loopStopWatch.Start();
					break;

				case PedalState.Record:
					_state = PedalState.Overdub;
					//Stop loopStopWatch and set loopLength
					_loopStopWatch.Stop();
					_loopLength = _loopStopWatch.Elapsed.TotalSeconds;
					_loopTimer.Interval = TimeSpan.FromSeconds(_loopLength / 60); //60 seconds per minute
					_loopTimer.Start();
					break;

				case PedalState.Overdub:
					_state = PedalState.Play;
					break;

				case PedalState.Play:
					_state = PedalState.Overdub;
					break;

				case PedalState.Stop:
					_state = PedalState.Play;
					guiProgress.Value = 0;
					_loopTimer.Start();
					break;
			}

			UpdateUI();
		}

		private void ToggleMuteTrack(int index = 0)
		{
			if (_mode == PedalMode.Record)
				return;

			_muteTrack[index] = !_muteTrack[index];
			UpdateUI();
		}

		private void SelectTrack(int index = 0)
		{
			if (_mode == PedalMode.Play)
				return;

			_selectedTrack = index;
			UpdateUI();
		}

		private void Clear()
		{
			_state = PedalState.Clear;

			_loopTimer.Stop();
			guiProgress.Value = 0;

			Stop();
			SendNotetoPedal(_midiNotes[0]);
			SelectTrack(0);
			ChangeMode(PedalMode.Record);

			for (int i = 0; i < _muteTrack.Length; i++)
			{
				if (_muteTrack[i]) //Track is MUTED
					_muteTrack[i] = false; //UNMUTE
			}

			_state = PedalState.None;
			UpdateUI();
		}

		private void MidiIn_MessageReceived(object? sender, MidiInMessageEventArgs e)
		{
			this.Dispatcher.Invoke(() => {
				NoteEvent _note = (NoteEvent)e.MidiEvent;
				if (_note.CommandCode == MidiCommandCode.NoteOn)
				{
					//_midiNotes = [40, 41, 43, 53, 55, 57, 59]
					/* _midiNotes[0] = CLEAR(E1)
					 * _midiNotes[1] = REC/PLAY(F1)
					 * _midiNotes[2] = STOP(G1)
					 * _midiNotes[3] = TRACK 1(0)/UNDO(+1)/MUTE(+12)/PLAY(+24)/OVERDUB(+25)
					 * _midiNotes[4] = TRACK 2(0)/UNDO(+1)/MUTE(+12)/PLAY(+24)/OVERDUB(+25)
					 * _midiNotes[5] = TRACK 3(0)/UNDO(+1)/MUTE(+12)/PLAY(+24)/OVERDUB(+25)
					 * _midiNotes[6] = TRACK 4(0)/UNDO(+1)/MUTE(+12)/PLAY(+24)/OVERDUB(+25)*/

					if (_note.NoteNumber == _midiNotes[0]) Clear(); //Clear
					if (_note.NoteNumber == _midiNotes[1]) ChangeState(); //Record/Overdub/Play
					if (_note.NoteNumber == _midiNotes[2]) Stop(); //Stop all
					if (_note.NoteNumber == _midiNotes[0] - 2) ChangeMode(); //RecMode/PlayMode

					//Track 1
					if (_note.NoteNumber == _midiNotes[3]) SelectTrack(0);
					if (_note.NoteNumber == _midiNotes[3] + 12) ToggleMuteTrack(0);

					//Track 2
					if (_note.NoteNumber == _midiNotes[4]) SelectTrack(1);
					if (_note.NoteNumber == _midiNotes[4] + 12) ToggleMuteTrack(1);

					//Track 3
					if (_note.NoteNumber == _midiNotes[5]) SelectTrack(2);
					if (_note.NoteNumber == _midiNotes[5] + 12) ToggleMuteTrack(2);

					//Track 4
					if (_note.NoteNumber == _midiNotes[6]) SelectTrack(3);
					if (_note.NoteNumber == _midiNotes[6] + 12) ToggleMuteTrack(3);
				}
			});
		}

		private void InputDeviceList_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			int deviceIndex = InputDeviceList.SelectedIndex;
			if (deviceIndex != -1)
			{
				ReleaseDevices();

				_inputDevice = new MidiIn(deviceIndex);
				_inputDevice.MessageReceived += MidiIn_MessageReceived;
				_inputDevice.Start();
			}
		}

		private void OutputDeviceList_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			int deviceIndex = OutputDeviceList.SelectedIndex;
			if (deviceIndex != -1)
			{
				ReleaseDevices();
				_outputPedal = new MidiOut(deviceIndex);
			}
		}

		protected override void OnClosing(CancelEventArgs e)
		{
			base.OnClosing(e);
			ReleaseDevices();
		}

		private void BtnScanDevices_Click(object sender, RoutedEventArgs e)
		{
			ScanForDevices();
		}

		private void BtnClear_Click(object sender, RoutedEventArgs e)
		{
			Clear();
			SendNotetoPedal(_midiNotes[0]);
			ChangeMode(PedalMode.Record);
		}

		private void BtnPlayRec_Click(object sender, RoutedEventArgs e)
		{
			if (!_stopWatch.IsRunning)
			{
				ChangeState();
				SendNotetoPedal(_midiNotes[1]);

				_stopWatch.Start();
			}
			else
			{
				if (_stopWatch.Elapsed.Seconds >= 0.5)
				{
					_stopWatch.Stop();
					_stopWatch.Reset();

					ChangeState();
					SendNotetoPedal(_midiNotes[1]);

					_stopWatch.Start();
				}
			}
		}

		private void BtnStop_Click(object sender, RoutedEventArgs e)
		{
			Stop();
			SendNotetoPedal(_midiNotes[2]);
		}

		private void BtnMode_Click(object sender, RoutedEventArgs e)
		{
			ChangeMode();
			SendNotetoPedal(_midiNotes[0] - 2);
		}

		private void BtnTrackOne_PreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
		{
			Trace.WriteLine("Mouse down TRACK 1");
			_stopWatch.Reset();
			_stopWatch.Start();

			if (_mode == PedalMode.Record)
			{
				SelectTrack(0);
				SendNotetoPedal(_midiNotes[3]);
			}
			else
			{
				ToggleMuteTrack(0);
				SendNotetoPedal(_midiNotes[3] + 12);
			}
		}
		
		private void BtnTrackOne_PreviewMouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
		{
			Trace.WriteLine("Mouse up TRACK 1");
			_stopWatch.Stop();
			_stopWatch.Reset();
		}

		private void BtnTrackTwo_PreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
		{
			Trace.WriteLine("Mouse down TRACK 2");
			if (_mode == PedalMode.Record)
			{
				SelectTrack(1);
				SendNotetoPedal(_midiNotes[4]);
			}
			else
			{
				ToggleMuteTrack(1);
				SendNotetoPedal(_midiNotes[4] + 12);
			}
		}

		private void BtnTrackTwo_PreviewMouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
		{
			Trace.WriteLine("Mouse up TRACK 2");
		}

		private void BtnTrackThree_PreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
		{
			Trace.WriteLine("Mouse down TRACK 3");
			if (_mode == PedalMode.Record)
			{
				SelectTrack(2);
				SendNotetoPedal(_midiNotes[5]);
			}
			else
			{
				ToggleMuteTrack(2);
				SendNotetoPedal(_midiNotes[5] + 12);
			}
		}

		private void BtnTrackThree_PreviewMouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
		{
			Trace.WriteLine("Mouse up TRACK 3");
		}

		private void BtnTrackFour_PreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
		{
			Trace.WriteLine("Mouse down TRACK 4");
			if (_mode == PedalMode.Record)
			{
				SelectTrack(3);
				SendNotetoPedal(_midiNotes[6]);
			}
			else
			{
				ToggleMuteTrack(3);
				SendNotetoPedal(_midiNotes[6] + 12);
			}
		}

		private void BtnTrackFour_PreviewMouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
		{
			Trace.WriteLine("Mouse up TRACK 4");
		}
	}
}