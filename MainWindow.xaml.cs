﻿using System.ComponentModel;
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

		private Border[] _trackLeds = new Border[4];
		private bool[] _muteTrack = new bool[4];
		private int _selectedTrack = 0; //0, 1; 2; 3;		
		private PedalMode _mode = PedalMode.Record;
		private PedalState _state = PedalState.None;

		private MidiIn? _device = null;

		public MainWindow()
		{
			InitializeComponent();

			//Get all midi devices connected and update listbox
			/*ICollection<Midi.InputDevice> d = Midi.InputDevice.GetAll();
			if (d.Count > 0)
			{
				foreach (Midi.InputDevice i in d)
				{
					this.DeviceList.Items.Add(i.Name);
				}
			}*/
			if(MidiIn.NumberOfDevices > 0)
			{
				for(int i = 0; i < MidiIn.NumberOfDevices; i++)
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
			
			if(_deviceIndex != -1)
			{
				if(_device != null)
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
			ledPlayRec.Background = Brushes.Transparent;

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

		private void BtnStop_Click(object sender, RoutedEventArgs e)
		{
			if (_state == PedalState.None)
				return;

			_state = PedalState.Stop;
			txtOutput.Text = _state.ToString();
			UpdateUI();
		}

		private void BtnMode_Click(object sender, RoutedEventArgs e)
		{
			_mode = (_mode == PedalMode.Record) ? PedalMode.Play : PedalMode.Record;
			txtOutput.Text = "Mode = " + ((_mode == PedalMode.Record) ? "recMode" : "playMode");
			UpdateUI();
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

		protected override void OnClosing(CancelEventArgs e)
		{
			base.OnClosing(e);

			//
		}

		private void midiIn_MessageReceived(object sender, MidiInMessageEventArgs e)
		{
			this.Dispatcher.Invoke(() => {
				NoteEvent _note = (NoteEvent)e.MidiEvent;
				txtOutput.Text = _note.NoteName;
			});
		}
	}
}