using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

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
		private readonly int[] _midiNotes = { 40, 41, 43, 53, 55, 57, 59 };

		private bool[] _muteTrack = new bool[4];
		private int _selectedTrack = 0; //0, 1; 2; 3;
		private PedalMode _mode = PedalMode.Record;
		private PedalState _state = PedalState.None;

		private MidiIn? _inputDevice = null;
		private MidiOut? _toPedal = null;

		private Brush _red = Brushes.Red;
		private Brush _orange = Brushes.Orange;
		private Brush _green = Brushes.Lime;
		private Brush _blue = Brushes.Blue;

		public MainWindow()
		{
			InitializeComponent();

			ScanForDevices();
			Clear();
			txtOutput.Text = string.Empty;
		}

		private void SendNotetoPedal(int noteNumber)
		{
			NoteOnEvent note = new NoteOnEvent(0, 1, noteNumber, 127, 0);

			if (_toPedal != null)
				_toPedal.Send(note.GetAsShortMessage());
		}

		private void ScanForDevices()
		{
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
			if (_mode == PedalMode.Record)
			{
				ledTrackOne.Background = (_selectedTrack == 0) ? _red : Brushes.Transparent;
				ledTrackTwo.Background = (_selectedTrack == 1) ? _red : Brushes.Transparent;
				ledTrackThree.Background = (_selectedTrack == 2) ? _red : Brushes.Transparent;
				ledTrackFour.Background = (_selectedTrack == 3) ? _red : Brushes.Transparent;
			}

			if (_mode == PedalMode.Play)
			{
				ledTrackOne.Background = (!_muteTrack[0]) ? _green : Brushes.Transparent;
				ledTrackTwo.Background = (!_muteTrack[1]) ? _green : Brushes.Transparent;
				ledTrackThree.Background = (!_muteTrack[2]) ? _green : Brushes.Transparent;
				ledTrackFour.Background = (!_muteTrack[3]) ? _green : Brushes.Transparent;
			}

			if(_state == PedalState.Clear)
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
		}

		private void ChangeMode(PedalMode newMode = PedalMode.Auto)
		{
			if (_state == PedalState.Clear)
				return;

			if(newMode == PedalMode.Auto)
				_mode = (_mode == PedalMode.Record) ? PedalMode.Play : PedalMode.Record;
			else
				_mode = newMode;

			UpdateUI();
		}

		private void Stop()
		{
			if (_state == PedalState.None)
				return;

			_state = PedalState.Stop;

			UpdateUI();
		}

		private void ChangeState()
		{
			if (_state == PedalState.None) //None
				_state = PedalState.Record; //switch to recMode
			else if (_state == PedalState.Record) //recMode
				_state = PedalState.Overdub; //switch to overMode
			else if (_state == PedalState.Overdub) //overMode
				_state = PedalState.Play; //switch to playMode
			else if (_state == PedalState.Play) //playMode
				_state = PedalState.Overdub; //switch to overMode
			else if (_state == PedalState.Stop) //stopMode
				_state = PedalState.Play; //switch to playMode

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

			Stop();
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

		private void midiIn_MessageReceived(object sender, MidiInMessageEventArgs e)
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

					//NEED TIMER ON REC/PLAY BTN!!!

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
				if (_inputDevice != null)
				{
					_inputDevice.Stop();
					_inputDevice.Dispose();
				}

				_inputDevice = new MidiIn(deviceIndex);
				_inputDevice.MessageReceived += midiIn_MessageReceived;
				_inputDevice.Start();
			}
		}

		private void OutputDeviceList_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			int deviceIndex = OutputDeviceList.SelectedIndex;
			if (deviceIndex != -1)
			{
				if (_toPedal != null)
					_toPedal.Dispose();

				if(deviceIndex != 0)
					_toPedal = new MidiOut(deviceIndex);
			}
		}

		protected override void OnClosing(CancelEventArgs e)
		{
			base.OnClosing(e);

			if(_inputDevice != null)
			{
				_inputDevice.Stop();
				_inputDevice.Dispose();
			}

			_toPedal?.Dispose();
		}

		private void BtnScanDevices_Click(object sender, RoutedEventArgs e)
		{
			ScanForDevices();
		}

		private void BtnPlayRec_Click(object sender, RoutedEventArgs e)
		{
			//THIS NEEDS A SLOW DOWN TIMER!!!

			ChangeState();
			SendNotetoPedal(_midiNotes[1]);
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

		private void BtnClear_Click(object sender, RoutedEventArgs e)
		{
			Clear();
			SendNotetoPedal(_midiNotes[0]);
			ChangeMode(PedalMode.Record);
		}

		private void BtnTrackOne_PreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
		{
			Trace.WriteLine("Mouse down TRACK 1");
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