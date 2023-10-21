using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Elf2Dsp1.Core;

namespace Elf2Dsp1
{
    public class DSP1
    {
        public enum SegmentType : byte
        {
            Prog0 = 0,
            Prog1 = 1,
            Data  = 2
        }

        public DSP1()
        {
            Header   = new DSP1Header();
            Segments = new DSP1Segment[0];
        }

        public DSP1(byte[] data)
        {
            using (var er = new EndianBinaryReader(new MemoryStream(data), Endianness.LittleEndian))
            {
                Header   = new DSP1Header(er);
                Segments = new DSP1Segment[Header.NrSegments];
                for (int i = 0; i < Header.NrSegments; i++)
                    Segments[i] = new DSP1Segment(er);
                for (int i = 0; i < Header.NrSegments; i++)
                {
                    er.BaseStream.Position  = Segments[i].Offset;
                    Segments[i].SegmentData = er.ReadBytes((int) Segments[i].Size);
                }
            }
        }

        public byte[] Write()
        {
            var m = new MemoryStream();
            using (var ew = new EndianBinaryWriter(m, Endianness.LittleEndian))
            {
                Header.NrSegments = (byte) Segments.Length;
                Header.Write(ew);
                uint dataOffset = 0x300;
                for (int i = 0; i < Segments.Length; i++)
                {
                    Segments[i].Offset = dataOffset;
                    Segments[i].Write(ew);
                    dataOffset += (uint) Segments[i].SegmentData.Length;
                }

                for (int i = 0; i < Segments.Length; i++)
                {
                    ew.BaseStream.Position = Segments[i].Offset;
                    ew.Write(Segments[i].SegmentData, 0, Segments[i].SegmentData.Length);
                }

                Header.FileSize = (uint) ew.BaseStream.Position;

                m.Position = 0;

                Header.Write(ew);

                return m.ToArray();
            }
        }

        public DSP1Header Header { get; set; }

        public class DSP1Header
        {
            public const uint DSP1Magic = 0x31505344;

            [Flags]
            public enum DSP1Flags : byte
            {
                SyncLoad      = 1,
                LoadFilterSeg = 2
            }

            public DSP1Header()
            {
                RsaSignature = new byte[0x100];
                Magic        = DSP1Magic;
                MemoryLayout = 0xFFFF;
            }

            public DSP1Header(EndianBinaryReader er)
            {
                RsaSignature = er.ReadBytes(0x100);
                Magic        = er.ReadUInt32();
                if (Magic != DSP1Magic)
                    throw new Exception("Invalid magic!");
                FileSize     = er.ReadUInt32();
                MemoryLayout = er.ReadUInt16();
                er.ReadUInt16();
                Unknown       = er.ReadByte();
                FilterSegType = (SegmentType) er.ReadByte();
                NrSegments    = er.ReadByte();
                Flags         = (DSP1Flags) er.ReadByte();
                FilterSegAddr = er.ReadUInt32();
                FilterSegSize = er.ReadUInt32();
                er.ReadUInt64();
            }

            public void Write(EndianBinaryWriter er)
            {
                er.Write(RsaSignature, 0, 0x100);
                er.Write(DSP1Magic);
                er.Write(FileSize);
                er.Write(MemoryLayout);
                er.Write((ushort) 0);
                er.Write(Unknown);
                er.Write((byte) FilterSegType);
                er.Write(NrSegments);
                er.Write((byte) Flags);
                er.Write(FilterSegAddr);
                er.Write(FilterSegSize);
                er.Write((ulong) 0);
            }

            public byte[]      RsaSignature  { get; set; }
            public UInt32      Magic         { get; set; }
            public UInt32      FileSize      { get; set; }
            public UInt16      MemoryLayout  { get; set; }
            public Byte        Unknown       { get; set; }
            public SegmentType FilterSegType { get; set; }
            public Byte        NrSegments    { get; set; }
            public DSP1Flags   Flags         { get; set; }
            public UInt32      FilterSegAddr { get; set; }
            public UInt32      FilterSegSize { get; set; }
        }

        public DSP1Segment[] Segments;

        public class DSP1Segment
        {
            public DSP1Segment()
            {
                Sha256 = new byte[32];
            }

            public DSP1Segment(EndianBinaryReader er)
            {
                Offset  = er.ReadUInt32();
                Address = er.ReadUInt32();
                Size    = er.ReadUInt32();
                er.ReadBytes(3);
                SegmentType = (SegmentType) er.ReadByte();
                Sha256      = er.ReadBytes(32);
            }

            public void Write(EndianBinaryWriter er)
            {
                er.Write(Offset);
                er.Write(Address);
                er.Write(SegmentData.Length);
                er.Write(new byte[3], 0, 3);
                er.Write((byte) SegmentType);
                Sha256 = SHA256.Create().ComputeHash(SegmentData);
                er.Write(Sha256, 0, 32);
            }

            public UInt32      Offset;
            public UInt32      Address;
            public UInt32      Size;
            public SegmentType SegmentType;
            public byte[]      Sha256;

            public byte[] SegmentData;
        }
    }
}