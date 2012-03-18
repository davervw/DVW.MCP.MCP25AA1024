/////////////////////////////////////////////////////////////////////////////////
// DVW.MCP.MCP25AA1024
// MCP25AA1024.cs
//
// Class for communicating with EEPROM model 25AA1024 from Microchip.  
//
// Copyright (c) 2012 Dave Van Wagner 
// http://techwithdave.blogspot.com/
//
// Microchip is a registered trademark of Microchip Technology Inc.
//
// See LICENSE.TXT for licensing terms

using System;
using System.Threading;
using Microsoft.SPOT;
using Microsoft.SPOT.Hardware;

namespace DVW.MCP
{
    public class MCP25AA1024
    {
        private enum Instruction { nop = 0x00, read = 0x03, write = 0x02, wren = 0x06, wrdi = 0x04, rdsr = 0x05, wrsr = 0x01, pe = 0x42, se = 0xd8, ce = 0xc7, rdid = 0xab, dpd = 0xb9 };
        private OutputPort Hold;
        private OutputPort WP;
        private SPI Spi;


        public MCP25AA1024(Cpu.Pin CS)
            :this(CS, 10000, SPI.SPI_module.SPI1, Cpu.Pin.GPIO_NONE, Cpu.Pin.GPIO_NONE)
        {
            // nothing more to construct
        }

        // Speed up to 20000 for 4.5V <= Vcc <= 5.5V
        // Speed up to 10000 for 2.5V <= Vcc < 4.5V
        // Speed up to  2000 for 1.8V <= Vcc < 2.5V
        public MCP25AA1024(Cpu.Pin CS, uint Speed, SPI.SPI_module SpiModule, Cpu.Pin Hold, Cpu.Pin WP)
        {
            Spi = new SPI(new SPI.Configuration((Cpu.Pin)CS, false, 1/*ms*/, 1/*ms*/, false, true, Speed/*KHz*/, SpiModule));

            if (Hold == Cpu.Pin.GPIO_NONE)
                this.Hold = null;
            else
                this.Hold = new OutputPort(Hold, true);

            if (WP == Cpu.Pin.GPIO_NONE)
                this.WP = null;
            else
                this.WP = new OutputPort(WP, true);

            if (Id != 0x29)
                throw new InvalidOperationException("Failed to communicate to EEPROM");
        }

        ~MCP25AA1024()
        {
            Spi.Dispose();
        }

        public void WaitForWriteInProgress()
        {
            while (WriteInProgress)
                Thread.Sleep(10);
        }

        public byte Read(int addr)
        {
            byte[] read_buffer = new byte[1];
            Read(addr, read_buffer);
            return read_buffer[0];
        }

        public void Read(int addr, byte[] read_buffer)
        {
            if ((addr + read_buffer.Length > 0x20000) || addr < 0)
                throw new InvalidOperationException("address out of range");

            byte[] write_buffer = new byte[4];
            write_buffer[0] = (byte)Instruction.read;
            write_buffer[1] = (byte)(addr >> 16);
            write_buffer[2] = (byte)(addr >> 8);
            write_buffer[3] = (byte)(addr);

            Spi.WriteRead(write_buffer, 0, write_buffer.Length, read_buffer, 0, read_buffer.Length, write_buffer.Length);
        }

        public void Write(int addr, byte[] buffer)
        {
            // break up into page size writes, and do not cross page boundries with individual writes
            int offset = 0;
            int len = buffer.Length;
            int max_size = 256-(byte)addr;
            int size = len;
            while (len > 0)
            {
                if (size > max_size)
                    size = max_size;
                Write(addr, buffer, offset, (byte)size);
                len -= size;
                offset += size;
                size = len;
                max_size = 256; // after first write can reset to full page size
            }

            WaitForWriteInProgress();
        }

        private void Write(int addr, byte[] buffer, int offset, byte size)
        {
            if ((addr + size > 0x20000) || addr < 0)
                throw new InvalidOperationException("address out of range");

            if (size > 0 && (addr >> 8) != ((addr + size - 1) >> 8))
                throw new InvalidOperationException("writes cannot overlap multiple pages");

            WaitForWriteInProgress();

            byte[] write_buffer = new byte[4 + size];
            write_buffer[0] = (byte)Instruction.write;
            write_buffer[1] = (byte)(addr >> 16);
            write_buffer[2] = (byte)(addr >> 8);
            write_buffer[3] = (byte)(addr);

            Array.Copy(buffer, offset, write_buffer, 4, size);
            Spi.Write(write_buffer);
        }

        public bool WriteEnable
        {
            get
            {
                if ((Status & 0x02) == 0x02)
                    return true;
                else
                    return false;
            }

            set
            { 
                byte[] write_buffer = new byte[1];
                if (value)
                    write_buffer[0] = (byte)Instruction.wren;
                else
                    write_buffer[0] = (byte)Instruction.wrdi;
                Spi.Write(write_buffer);
            }
        }

        public bool WriteInProgress
        {
            get
            {
                if ((Status & 0x01) == 0x01)
                    return true;
                else
                    return false;
            }
        }

        // get/set block write protection bits
        // 0 = all sectors (0, 1, 2, & 3) unprotected 0x00000-0x1FFFF
        // 1 = sector 3 protected 0x18000-0x1FFFF, remaining sectors (0, 1, & 2) unprotected 0x00000-0x17FFF
        // 2 = sectors 2, 3 protected 0x10000-0x1FFFF, while sectors 0, 1 unprotected 0x00000-0x0FFFF
        // 3 = all sectors (0, 1, 2, & 3) protected 0x00000-0x1FFFF
        public byte BP
        {
            get
            {
                return (byte)((Status >> 2) & 0x3);
            }

            set
            {
                Status = (byte)((Status & 0xF3) | ((value & 0x03) << 2));
            }
        }

        public bool WriteProtectEnable
        {
            get
            {
                if ((Status & 0x80) == 0x80)
                    return true;
                else
                    return false;
            }

            set
            {
                Status = (byte)((Status & 0x7F) | (value ? 0x80 : 0x00));
            }
        }

        public byte Status
        {
            get
            {
                byte[] write_buffer = new byte[1];
                write_buffer[0] = (byte)Instruction.rdsr;
                byte[] read_buffer = new byte[1];
                Spi.WriteRead(write_buffer, 0, write_buffer.Length, read_buffer, 0, read_buffer.Length, write_buffer.Length);
                return read_buffer[0];
            }

            set
            {
                byte[] write_buffer = new byte[2];
                write_buffer[0] = (byte)Instruction.wrsr;
                write_buffer[1] = value;
                Spi.Write(write_buffer);
                WaitForWriteInProgress();
            }
        }

        public void PageErase(int addr)
        {
            byte[] write_buffer = new byte[4];
            write_buffer[0] = (byte)Instruction.pe;
            write_buffer[1] = (byte)(addr >> 16);
            write_buffer[2] = (byte)(addr >> 8);
            write_buffer[3] = (byte)(addr);

            Spi.Write(write_buffer);

            WaitForWriteInProgress();
        }

        public void SectorErase(int addr)
        {
            byte[] write_buffer = new byte[4];
            write_buffer[0] = (byte)Instruction.se;
            write_buffer[1] = (byte)(addr >> 16);
            write_buffer[2] = (byte)(addr >> 8);
            write_buffer[3] = (byte)(addr);

            Spi.Write(write_buffer);

            WaitForWriteInProgress();
        }

        public void ChipErase()
        {
            byte[] write_buffer = new byte[1];
            write_buffer[0] = (byte)Instruction.ce;

            Spi.Write(write_buffer);

            WaitForWriteInProgress();
        }

        public void DeepPowerDown()
        {
            byte[] write_buffer = new byte[1];
            write_buffer[0] = (byte)Instruction.dpd;

            Spi.Write(write_buffer);
        }

        public byte ReleasePowerDown()
        {
            byte[] write_buffer = new byte[4];
            write_buffer[0] = (byte)Instruction.rdid;
            write_buffer[1] = 0;
            write_buffer[2] = 0;
            write_buffer[3] = 0;

            byte[] read_buffer = new byte[1];
            Spi.WriteRead(write_buffer, 0, write_buffer.Length, read_buffer, 0, read_buffer.Length, write_buffer.Length);
            return read_buffer[0];
        }

        public byte Id
        {
            get
            {
                return ReleasePowerDown();
            }
        }
    }
}
