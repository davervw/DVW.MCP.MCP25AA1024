/////////////////////////////////////////////////////////////////////////////////
// MCP_EEPROM_test.cc
//
// Test application for DVW.MCP.MCP25AA1024 class
//
// Copyright (c) 2012 Dave Van Wagner 
// http://techwithdave.blogspot.com/
//
// See LICENSE.TXT for licensing terms

using System;
using System.Threading;
using DVW.MCP;
using GHIElectronics.NETMF.FEZ; // FEZ only
using Microsoft.SPOT;
using Microsoft.SPOT.Hardware;
//using SecretLabs.NETMF.Hardware.NetduinoPlus; // Netduino only

namespace MCP25AA1024_test
{
    public class MCP_EEPROM_test
    {
        public static void Blink()
        {
            // Blink board LED

            bool ledState = false;

            OutputPort led = new OutputPort((Cpu.Pin)FEZ_Pin.Digital.LED, ledState); // FEZ only
            //OutputPort led = new OutputPort(Pins.ONBOARD_LED, ledState); // Netduino and Netduino Plus only

            while (true)
            {
                // Sleep for 500 milliseconds
                Thread.Sleep(250);

                // toggle LED state
                ledState = !ledState;
                led.Write(ledState);
            }
        }

        public static void Main()
        {
            MCP25AA1024 eeprom = new MCP25AA1024((Cpu.Pin)FEZ_Pin.Digital.Di9); // FEZ Panda II only
            //MCP25AA1024 eeprom = new MCP25AA1024(Pins.GPIO_PIN_D2); // Netduino or Netduino Plus

            Thread thread = new Thread(Blink);
            thread.Start();

            Debug.Print("ID = " + eeprom.Id.ToString());
            Debug.Print("Status = " + eeprom.Status.ToString());
            Debug.Print("WriteEnable = " + eeprom.WriteEnable.ToString());

            Debug.Print("Reading from EEPROM");

            byte[] read_buffer = new byte[12];
            eeprom.Read(0, read_buffer);
            for (int i = 0; i < read_buffer.Length; ++i)
                Debug.Print(((Char)read_buffer[i]).ToString());

            if (read_buffer[0] != 'H')
            {
                Debug.Print("Writing to EEPROM");

                byte[] write_buffer = { 
                                      (byte)'H',
                                      (byte)'e',
                                      (byte)'l',
                                      (byte)'l',
                                      (byte)'o',
                                      (byte)' ',
                                      (byte)'E',
                                      (byte)'E',
                                      (byte)'P',
                                      (byte)'R',
                                      (byte)'O',
                                      (byte)'M',
                                  };
                eeprom.WriteEnable = true;
                eeprom.Write(0, write_buffer);
                eeprom.WriteEnable = false;
            }
            //else
            //{
            //    eeprom.WriteEnable = true;
            //    Debug.Print("WriteEnable = " + eeprom.WriteEnable.ToString());
            //    Debug.Print("WriteProtectEnable = " + eeprom.WriteProtectEnable.ToString());
            //    eeprom.WriteProtectEnable = !eeprom.WriteProtectEnable;
            //    Debug.Print("WriteProtectEnable = " + eeprom.WriteProtectEnable.ToString());
            //}

            Debug.Print("Done");
        }

    }
}
