using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xamarin.Forms;
using static Xamarin.Essentials.Permissions;
using XamarinEssentials = Xamarin.Essentials;
using Plugin.BLE;
using Plugin.BLE.Abstractions;
using Plugin.BLE.Abstractions.Contracts;
using Xamarin.Essentials;
using SkiaChart;
using SkiaChart.Charts;
using System.Diagnostics;

namespace StrRobot
{
    public partial class MainPage : TabbedPage
    {
        private readonly IAdapter _bluetoothAdapter;                            // Class for the Bluetooth adapter
        private readonly List<IDevice> _gattDevices = new List<IDevice>();
        private IDevice selectedItem;

        
        Steer_Status status = new Steer_Status();
        byte[] command = new byte[5];
        public MainPage()
        {
            InitializeComponent();

            _bluetoothAdapter = CrossBluetoothLE.Current.Adapter;               // Point _bluetoothAdapter to the current adapter on the phone
            _bluetoothAdapter.DeviceDiscovered += (sender, foundBleDevice) =>   // When a BLE Device is found, run the small function below to add it to our list
            {
                if (foundBleDevice.Device != null && !string.IsNullOrEmpty(foundBleDevice.Device.Name))
                    _gattDevices.Add(foundBleDevice.Device);
            };
            Disconnect_Button.IsEnabled = false;
            SCAN_Button.IsEnabled = true;
        }

        private async Task<bool> PermissionsGrantedAsync()      // Function to make sure that all the appropriate approvals are in place
        {
            var status = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();

            if (status != PermissionStatus.Granted)
            {
                status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
            }

            return status == PermissionStatus.Granted;
        }


        private async void SCAN_Button_Clicked(object sender, EventArgs e)           // Function that is called when the SCAN_Button is pressed
        {
            IsBusyIndicator.IsVisible = IsBusyIndicator.IsRunning = !(SCAN_Button.IsEnabled = false);        // Swith the Isbusy Indicator on
            FoundBLEDevice.ItemsSource = null;                                                     // Empty the list of found BLE devices (in the GUI)

            if (!await PermissionsGrantedAsync())                                                           // Make sure there is permission to use Bluetooth
            {
                await DisplayAlert("Permission required", "Application needs location permission", "OK");
                IsBusyIndicator.IsVisible = IsBusyIndicator.IsRunning = !(SCAN_Button.IsEnabled = true);
                return;
            }

            _gattDevices.Clear();                                                                           // Also clear the _gattDevices list

            if (!_bluetoothAdapter.IsScanning)                                                              // Make sure that the Bluetooth adapter is scanning for devices
            {
                await _bluetoothAdapter.StartScanningForDevicesAsync();
            }

            foreach (var device in _bluetoothAdapter.ConnectedDevices)                                      // Make sure BLE devices are added to the _gattDevices list
                _gattDevices.Add(device);

            FoundBLEDevice.ItemsSource = _gattDevices.ToArray();                                   // Write found BLE devices to GUI
            IsBusyIndicator.IsVisible = IsBusyIndicator.IsRunning = !(SCAN_Button.IsEnabled = true);         // Switch off the busy indicator
        }

        private async void BLE_List(object sender, ItemTappedEventArgs e)
        {
            IsBusyIndicator.IsVisible = IsBusyIndicator.IsRunning = !(SCAN_Button.IsEnabled = false);        // Switch on IsBusy indicator
            selectedItem = e.Item as IDevice;                                                       // The item selected is an IDevice (detected BLE device). Therefore we have to cast the selected item to an IDevice
            
            Disconnect_Button.IsEnabled = true;
            if (selectedItem.State != DeviceState.Connected)
            {
                try
                {
                    var connectParameters = new ConnectParameters(false, true);
                    await _bluetoothAdapter.ConnectToDeviceAsync(selectedItem, connectParameters);          // if we are not connected, then try to connect to the BLE Device selected

                }
                catch
                {
                    await DisplayAlert("Error connecting", $"Error connecting to BLE device: {selectedItem.Name ?? "N/A"}", "Retry");       // give an error message if it is not possible to connect
                }
            }

            IsBusyIndicator.IsVisible = IsBusyIndicator.IsRunning = !(SCAN_Button.IsEnabled = true);         // switch off the "Isbusy" indicator
            SCAN_Button.IsEnabled = false;

            //await Task.Run(async () =>
            //{
            //    await ReceiveDataInBackground(selectedItem);
            //});


        }
        private async void Disconnect_Button_Clicked(object sender, EventArgs e)
        {
            IDevice selectedDevice = selectedItem;
            if (selectedDevice != null)
            {
                await DisconnectDevice(selectedDevice);
                SCAN_Button.IsEnabled=true;
                Disconnect_Button.IsEnabled=false;

            }
        }

        private async void L2L_Clicked(object sender, EventArgs e)
        {
            command[0] = status.L2L; //L2L Mode
            float rawtorque = float.Parse(Input_Torque.Text);
            int torque = (int)(rawtorque / status.Kt * 2000 / 32 / status.gear_ratio);
            command[1] = (byte)(torque & 0xff);
            command[2] = (byte)((torque >> 8) & 0xff);
            command[3] = status.stuck_angle;
            command[4] = status.stuck_time;
            await SendDataToDevice(selectedItem, command);
        }
        private async void CsntStr(object sender, EventArgs e)
        {
            command[0] = status.ConstStr;
            command[1] = (byte)((int)float.Parse(Velocity.Text)*100);
            command[2] = byte.Parse(ConstCycle.Text);
            command[3] = (byte)(int.Parse(Range.Text) * 2);
            command[4] = 0;
            await SendDataToDevice(selectedItem, command);
        }
        private async void SineStr(object sender, EventArgs e)
        {
            var angle = int.Parse(Angle.Text);
            var amp = int.Parse(Amplitude.Text);
            var fre = float.Parse(Frequency.Text);
            var cycle = byte.Parse(SineCycle.Text);
            command[0] = status.SineStr;
            command[1] = (byte)(fre * 100);
            command[2] = cycle;
            command[3] = (byte)(200 - angle * 2);
            command[4] = (byte)(amp / 2);
            await SendDataToDevice(selectedItem, command);
        }
        private async void Pause(object sender, EventArgs e)
        {
            if (command[0] != status.Pause)
            {
                command[0] = status.Pause;
            }
            else
            {
                command[0] = status.Restart;
            }
            for (int i = 1; i < command.Length; i++)
            {
                command[i] = 0;
            }
            await SendDataToDevice(selectedItem, command);

        }
        
        private async void Return_to_zero(object sender, EventArgs e)
        { 
            command[0] = status.GOREADY;
            for(int i=1; i<command.Length; i++)
            {
                command[i] = 0;
            }
            await SendDataToDevice(selectedItem, command);
        }
        private async void Motor_OFF(object sender, EventArgs e)
        {
            command[0] = status.mtrOff;
            for (int i = 1; i < command.Length; i++)
            {
                command[i] = 0;
            }
            await SendDataToDevice(selectedItem, command);
        }
        private async Task DisconnectDevice(IDevice device)
        {
            // Check if the device is connected
            if (device.State == DeviceState.Connected)
            {
                // Get the BLE connection for the devic

                // Disconnect the device
                await _bluetoothAdapter.DisconnectDeviceAsync(device);
            }
        }

    private async Task SendDataToDevice(IDevice device, byte[] data)
{
    // Check if the device is connected
    if (device.State == DeviceState.Connected)
    {
        try
        {
            var service2 = await device.GetServiceAsync(Guid.Parse("a301c36d-71c2-4708-93aa-77d81c405d6c"));
            if (service2 != null)
            {
                // Get the characteristic of the service (you may need to adjust this based on your specific use case)
                var characteristic2 = await service2.GetCharacteristicAsync(Guid.Parse("a291577c-b20c-48ff-91f2-b51f85f936ba"));
                if (characteristic2 != null)
                {
                            try
                            {
                                await characteristic2.WriteAsync(data);
                                Console.WriteLine("Data sent successfully");
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Failed to send data: {ex.Message}");
                            }
                        }
                else
                {
                    await DisplayAlert("Error sending data", "Characteristic not found", "OK");
                }
            }
            else
            {
                await DisplayAlert("Error sending data", "Service not found", "OK");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error sending data", $"Error sending data: {ex.Message}", "OK");
        }
    }
    else
    {
        await DisplayAlert("Error sending data", "Device not connected", "OK");
    }
}
        private async Task ReceiveDataInBackground(IDevice device)
        {
            while (true)
            {
                 await ReceiveDataFromDevice(device);

                // Optional: Add delay between each data reception to control the rate of data retrieval
                await Task.Delay(10); // 1 second delay
            }
        }
        private async Task ReceiveDataFromDevice(IDevice device)
        {
            // Check if the device is connected
            if (device.State == DeviceState.Connected)
            {
                try
                {
                    var service = await device.GetServiceAsync(Guid.Parse("7d048732-c35e-4402-ad3d-6c752b88352d"));
                    if (service != null)
                    {
                        var characteristic = await service.GetCharacteristicAsync(Guid.Parse("85ab27ad-9cd0-4c2d-9c42-942bfe7baf57"));
                        if (characteristic != null)
                        {
                            characteristic.ValueUpdated += Characteristic_ValueUpdated;
                            await characteristic.StartUpdatesAsync();
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error receiving data: {ex.Message}");
                }
            }
        }

        private void Characteristic_ValueUpdated(object sender, Plugin.BLE.Abstractions.EventArgs.CharacteristicUpdatedEventArgs e)
        {
            // Handle the received data
            byte[] dataBytes = e.Characteristic.Value;
            //int receivedData = BitConverter.ToInt16(dataBytes, 0);

            // Update UI with the received data
            Device.InvokeOnMainThreadAsync(() =>
            {
                SETTINGLable.Text = dataBytes[0].ToString();
            });
        }

    }
}
