using System.ComponentModel;
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
		private enum PedalMode { Record, Play };
		private enum PedalState { None, Record, Overdub, Play, Stop };
		private readonly int[] _midiNotes = { 40, 41, 43, 53, 55, 57, 59 };

		private Border[] _trackLeds = new Border[4];
		private bool[] _muteTrack = new bool[4];
		private int _selectedTrack = 0; //0, 1; 2; 3;		
		private PedalMode _mode = PedalMode.Record;
		private PedalState _state = PedalState.None;

		private MidiIn? _device = null;

		public MainWindow()
		{
			InitializeComponent();

			if (MidiIn.NumberOfDevices > 0)
			{
				for (int i = 0; i < MidiIn.NumberOfDevices; i++)
				{
					DeviceList.Items.Add(MidiIn.DeviceInfo(i).ProductName);
				}
			}

			_trackLeds[0] = ledTrackOne;
			_trackLeds[1] = ledTrackTwo;
			_trackLeds[2] = ledTrackThree;
			_trackLeds[3] = ledTrackFour;

			Clear();
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

		private void ChangeMode()
		{
			_mode = (_mode == PedalMode.Record) ? PedalMode.Play : PedalMode.Record;
			txtOutput.Text = "Mode = " + ((_mode == PedalMode.Record) ? "recMode" : "playMode");
			UpdateUI();
		}

		private void Stop()
		{
			if (_state == PedalState.None)
				return;

			_state = PedalState.Stop;
			txtOutput.Text = _state.ToString();
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

			txtOutput.Text = _state.ToString();
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

			txtOutput.Text = "MuteTrack(" + index + ") = " + _muteTrack[index];
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

			txtOutput.Text = "SelectTrack(" + index + ")";
			UpdateUI();
		}

		private void Clear()
		{
			_state = PedalState.None; //None
			_mode = PedalMode.Record; //recMode
			SelectTrack(0);

			for (int i = 0; i < _muteTrack.Length; i++)
			{
				if (_muteTrack[i]) //Track is MUTED
					_muteTrack[i] = false; //UNMUTE
			}

			txtOutput.Text = "CLEAR";
			UpdateUI();
		}

		private void DeviceList_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			int _deviceIndex = DeviceList.SelectedIndex;

			if (_deviceIndex != -1)
			{
				if (_device != null)
				{
					_device.Stop();
					_device.Dispose();
				}

				_device = new MidiIn(_deviceIndex);
				_device.MessageReceived += midiIn_MessageReceived;
				_device.Start();
			}

			txtOutput.Text = "Selected device" + _deviceIndex.ToString();
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
					//Notes from pico pedal
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
					if (_note.NoteNumber == _midiNotes[2]) { Stop(); } //Stop all
					if (_note.NoteNumber == 45) { ChangeMode(); } //RecMode/PlayMode

					//Track 1
					if (_note.NoteNumber == _midiNotes[3]) SelectTrack(0); //Select track 1
					if (_note.NoteNumber == _midiNotes[3] + 12) { ToggleMuteTrack(0); } //Toggle mute track 1

					if (_note.NoteNumber == _midiNotes[4]) SelectTrack(1); //Select track 2
					if (_note.NoteNumber == _midiNotes[4] + 12) { ToggleMuteTrack(1); } //Toggle mute track 2

					if (_note.NoteNumber == _midiNotes[5]) SelectTrack(2); //Select track 3
					if (_note.NoteNumber == _midiNotes[5] + 12) { ToggleMuteTrack(2); } //Toggle mute track 3

					if (_note.NoteNumber == _midiNotes[6]) SelectTrack(3); //Select track 4								
					if (_note.NoteNumber == _midiNotes[6] + 12) { ToggleMuteTrack(3); }  //Toggle mute track 4
				}
			});
		}

		private void BtnMinimise_Click(object sender, RoutedEventArgs e)
		{
			WindowState = WindowState.Minimized;
		}

		private void BtnClose_Click(object sender, RoutedEventArgs e)
		{
			if(_device != null)
			{
				_device.MessageReceived -= midiIn_MessageReceived;
				_device.Stop();
				_device.Dispose();
			}
			
			Application.Current.Shutdown();
		}

		protected override void OnClosing(CancelEventArgs e)
		{
			base.OnClosing(e);

			//
		}
	}
}