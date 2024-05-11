using System;
using System.Reflection;
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
		private enum PedalState { None, Record, Overdub, Play, Stop };
		private readonly int[] _midiNotes = { 40, 41, 43, 53, 55, 57, 59 };

		private Border[] _trackLeds = new Border[4];
		private bool[] _muteTrack = new bool[4];
		private int _selectedTrack = 0; //0, 1; 2; 3;		
		private PedalMode _mode = PedalMode.Record;
		private PedalState _state = PedalState.None;

		private MidiIn? _fromPedal = null;
		private MidiOut? _toPedal = null;

		public MainWindow()
		{
			InitializeComponent();

			_trackLeds[0] = ledTrackOne;
			_trackLeds[1] = ledTrackTwo;
			_trackLeds[2] = ledTrackThree;
			_trackLeds[3] = ledTrackFour;

			ScanDevices();
			Clear();
		}

		private void SendNotetoPedal(int noteNumber)
		{
			if (_toPedal != null)
			{
				NoteOnEvent note = new NoteOnEvent(0, 1, noteNumber, 127, 0);
				_toPedal.Send(note.GetAsShortMessage());
			}
		}

		private void ScanDevices()
		{
			if (MidiIn.NumberOfDevices > 0)
			{
				InputDeviceList.Items.Clear();

				for (int i = 0; i < MidiIn.NumberOfDevices; i++)
				{
					InputDeviceList.Items.Add(MidiIn.DeviceInfo(i).ProductName);
				}
			}

			if (MidiOut.NumberOfDevices > 0)
			{
				OutputDeviceList.Items.Clear();

				for (int i = 0; i < MidiOut.NumberOfDevices; i++)
				{
					OutputDeviceList.Items.Add(MidiOut.DeviceInfo(i).ProductName);
				}
			}
		}

		private void UpdateUI()
		{
			//recMode
			if (_mode == PedalMode.Record)
			{
				for (int i = 0; i < _trackLeds.Length; i++)
					_trackLeds[i].Background = (i == _selectedTrack) ? Brushes.Red : Brushes.Transparent;
			}

			//playMode
			if (_mode == PedalMode.Play)
			{
				for (int i = 0; i < _trackLeds.Length; i++)
					_trackLeds[i].Background = (_muteTrack[i]) ? Brushes.Transparent : Brushes.Lime;
			}

			//Update state led
			ledPlayRec.Background = Brushes.Transparent; //none (default)
			if (_state == PedalState.Record) ledPlayRec.Background = Brushes.Red; //recMode
			if (_state == PedalState.Overdub) ledPlayRec.Background = Brushes.Orange; //overMode
			if (_state == PedalState.Play) ledPlayRec.Background = Brushes.Lime; //playMode
			ledStop.Background = (_state == PedalState.Stop) ? Brushes.Blue : Brushes.Transparent; //stopMode
		}

		private void ChangeMode(PedalMode newMode = PedalMode.Auto)
		{
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
			//if in recMode
			if (_mode == PedalMode.Record)
				return;

			//playMode
			_muteTrack[index] = !_muteTrack[index];
			_trackLeds[index].Background = (_muteTrack[index]) ? Brushes.Transparent : Brushes.Lime;

			UpdateUI();
		}

		private void SelectTrack(int index = 0)
		{
			//if in playMode
			if (_mode == PedalMode.Play)
				return;

			_selectedTrack = index;

			for (int i = 0; i < _trackLeds.Length; i++)
				_trackLeds[i].Background = (i == index) ? Brushes.Red : Brushes.Transparent;

			SendNotetoPedal(_midiNotes[index + 3]);
			UpdateUI();
		}

		private void Clear()
		{
			Stop();
			_state = PedalState.None; //None
			_mode = PedalMode.Record;
			SelectTrack(0);

			for (int i = 0; i < _muteTrack.Length; i++)
			{
				if (_muteTrack[i]) //Track is MUTED
					_muteTrack[i] = false; //UNMUTE
			}

			UpdateUI();
		}

		private void InputDeviceList_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			int deviceIndex = InputDeviceList.SelectedIndex;
			if (deviceIndex != -1)
			{
				if(_fromPedal != null)
				{
					_fromPedal.Stop();
					_fromPedal.Dispose();
				}

				_fromPedal = new MidiIn(deviceIndex);
				_fromPedal.MessageReceived += midiIn_MessageReceived;
				_fromPedal.Start();
			}
		}

		private void OutputDeviceList_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			int deviceIndex = OutputDeviceList.SelectedIndex;
			if(deviceIndex != -1)
			{
				if (_toPedal != null)
					_toPedal.Dispose();

				_toPedal = new MidiOut(deviceIndex);
			}
		}

		private void BtnPlayRec_Click(object sender, RoutedEventArgs e)
		{
			ChangeState();
		}

		private void BtnStop_Click(object sender, RoutedEventArgs e)
		{
			Stop();
		}

		private void BtnMode_Click(object sender, RoutedEventArgs e)
		{
			ChangeMode();
		}

		private void BtnClear_Click(object sender, RoutedEventArgs e)
		{
			Clear();
		}

		private void BtnTrackOne_Click(object sender, RoutedEventArgs e)
		{
			if (_mode == PedalMode.Record)
			{
				SelectTrack(0);
			}
			else
			{
				ToggleMuteTrack(0);
			}
		}

		private void BtnTTrackTwo_Click(object sender, RoutedEventArgs e)
		{
			if (_mode == PedalMode.Record)
			{
				SelectTrack(1);
			}
			else
			{
				ToggleMuteTrack(1);
			}
		}

		private void BtnTrackThree_Click(object sender, RoutedEventArgs e)
		{
			if (_mode == PedalMode.Record)
			{
				SelectTrack(2);
			}
			else
			{
				ToggleMuteTrack(2);
			}
		}

		private void BtnTrackFour_Click(object sender, RoutedEventArgs e)
		{
			if (_mode == PedalMode.Record)
			{
				SelectTrack(3);
			}
			else
			{
				ToggleMuteTrack(3);
			}
		}

		private void midiIn_MessageReceived(object sender, MidiInMessageEventArgs e)
		{
			this.Dispatcher.Invoke(() =>
			{
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

		private void BtnMinimise_Click(object sender, RoutedEventArgs e)
		{
			WindowState = WindowState.Minimized;
		}

		private void BtnClose_Click(object sender, RoutedEventArgs e)
		{
			if (_fromPedal != null)
			{
				_fromPedal.MessageReceived -= midiIn_MessageReceived;
				_fromPedal.Stop();
				_fromPedal.Dispose();
			}

			_toPedal?.Dispose();

			Application.Current.Shutdown();
		}

		private void BtnScanDevices_Click(object sender, RoutedEventArgs e)
		{
			ScanDevices();
		}
	}
}