using System.Collections.Generic;
using System.IO;
using System.Text;
using Elf2Dsp1.Core;

namespace Elf2Dsp1.ELF.Dwarf2
{
    public class DW2DebugLine
    {
        private readonly byte[] _debugLineData;

        private enum DwLnsStandardOp
        {
            Copy = 1,
            AdvancePc,
            AdvanceLine,
            SetFile,
            SetColumn,
            NegateStmt,
            SetBasicBlock,
            ConstAddPc,
            FixedAdvancePc
        }

        private enum DwLnsExtendedOp
        {
            EndSequence = 1,
            SetAddress,
            DefineFile
        }

        public DW2DebugLine(byte[] data)
        {
            _debugLineData = data;
        }

        private string[] ReadStringTable(EndianBinaryReader er)
        {
            List<string> result = new List<string>();
            string cur = "";
            while (true)
            {
                string str = er.ReadStringNT(Encoding.ASCII);
                if (str == "")
                    break;
                result.Add(str);
            }
            return result.ToArray();
        }

        private DW2FileEntry[] ReadFileEntries(EndianBinaryReader er)
        {
            List<DW2FileEntry> result = new List<DW2FileEntry>();
            while (true)
            {
                byte b = er.ReadByte();
                if (b == 0)
                    break;
                er.BaseStream.Position--;
                result.Add(new DW2FileEntry(er));
            }
            return result.ToArray();
        }

        private delegate bool LookupCallback(string[] dirs, DW2FileEntry[] files, List<DW2StateMachine> matrixEntries);

        private void LookupWithCallback(LookupCallback callback)
        {
            EndianBinaryReader er = new EndianBinaryReader(new MemoryStream(_debugLineData), Endianness.LittleEndian);
            while (er.BaseStream.Position < er.BaseStream.Length)
            {
                List<DW2StateMachine> matrixEntries = new List<DW2StateMachine>();
                long start = er.BaseStream.Position;
                DW2DebugLineHeader header = new DW2DebugLineHeader(er);
                //read the prologue
                string[] dirs = ReadStringTable(er);
                DW2FileEntry[] files = ReadFileEntries(er);
                DW2StateMachine stateMachine = new DW2StateMachine(header.DefaultIsStatement);
                uint baseAddress = 0;
                while (er.BaseStream.Position - start < header.Length + 4)
                {
                    byte op = er.ReadByte();
                    if (op < header.OpcodeBase)
                    {
                        if (op == 0)
                        {
                            uint instLength = IOUtil.ReadUnsignedLeb128(er);
                            if (instLength > 0)
                            {
                                byte inst = er.ReadByte();
                                if (inst > 0 && inst <= 3)
                                {
                                    switch ((DwLnsExtendedOp)inst)
                                    {
                                        case DwLnsExtendedOp.EndSequence:
                                            stateMachine.EndSequence = true;
                                            matrixEntries.Add(stateMachine);
                                            stateMachine = new DW2StateMachine(header.DefaultIsStatement);
                                            break;
                                        case DwLnsExtendedOp.SetAddress:
                                            stateMachine.Address = baseAddress = er.ReadUInt32();
                                            break;
                                        default:
                                            break;
                                    }
                                }
                                else
                                {
                                    er.BaseStream.Position -= 1;
                                    er.BaseStream.Position += instLength;
                                }
                            }
                            else
                            {

                            }
                        }
                        else if (op <= 9)
                        {
                            switch ((DwLnsStandardOp)op)
                            {
                                case DwLnsStandardOp.Copy:
                                    matrixEntries.Add(stateMachine);
                                    stateMachine.BasicBlock = false;
                                    break;
                                case DwLnsStandardOp.AdvancePc:
                                    stateMachine.Address += IOUtil.ReadUnsignedLeb128(er) * header.MinInstructionLength;
                                    break;
                                case DwLnsStandardOp.AdvanceLine:
                                    stateMachine.Line = (uint)(stateMachine.Line + IOUtil.ReadSignedLeb128(er));
                                    break;
                                case DwLnsStandardOp.SetFile:
                                    stateMachine.File = IOUtil.ReadUnsignedLeb128(er);
                                    break;
                                case DwLnsStandardOp.SetColumn:
                                    stateMachine.Column = IOUtil.ReadUnsignedLeb128(er);
                                    break;
                                case DwLnsStandardOp.NegateStmt:
                                    stateMachine.IsStmt = !stateMachine.IsStmt;
                                    break;
                                case DwLnsStandardOp.SetBasicBlock:
                                    stateMachine.BasicBlock = true;
                                    break;
                                default:
                                    break;
                            }
                        }
                        else
                        {

                        }
                    }
                    else
                    {
                        op -= header.OpcodeBase;
                        int addrInc = op / header.LineRange;
                        int lineInc = header.LineBase + (op % header.LineRange);
                        stateMachine.Address = (uint)(stateMachine.Address + addrInc * header.MinInstructionLength);
                        stateMachine.Line = (uint)(stateMachine.Line + lineInc);
                        matrixEntries.Add(stateMachine);
                        stateMachine.BasicBlock = false;
                    }
                }
                if (callback(dirs, files, matrixEntries))
                    break;
                er.BaseStream.Position = start + header.Length + 4;
            }
            er.Close();
        }

        public AddressLookupResult? Lookup(uint address)
        {
            AddressLookupResult? result = null;
            LookupWithCallback((dirs, files, matrixEntries) =>
            {
                DW2StateMachine? entry = null;
                for (int i = 0; i < matrixEntries.Count; i++)
                {
                    if (matrixEntries[i].Address == address)
                    {
                        entry = matrixEntries[i];
                        break;
                    }
                    else if (i > 0 && matrixEntries[i - 1].Address > address - 0x20 && matrixEntries[i - 1].Address < address && matrixEntries[i].Address > address)
                    {
                        entry = matrixEntries[i - 1];
                    }
                }
                if (entry != null)
                {
                    DW2StateMachine actualEntry = entry.Value;
                    result = new AddressLookupResult()
                    {
                        Address = actualEntry.Address,
                        Column = (int)actualEntry.Column,
                        Directory = dirs[files[actualEntry.File - 1].DirectoryIndex - 1],
                        File = files[actualEntry.File - 1].FileName,
                        Line = (int)actualEntry.Line
                    };
                    return true;
                }
                return false;
            });
            return result;
        }

        public uint? Lookup(string file, int line)
        {
            uint? result = null;
            LookupWithCallback((dirs, files, matrixEntries) =>
            {
                DW2StateMachine? entry = null;
                for (int i = 0; i < matrixEntries.Count; i++)
                {
                    if (files.Length > (matrixEntries[i].File - 1) &&
                        dirs[files[matrixEntries[i].File - 1].DirectoryIndex - 1] +
                        files[matrixEntries[i].File - 1].FileName == file)
                    {
                        if (matrixEntries[i].Line == line)
                        {
                            entry = matrixEntries[i];
                            break;
                        }
                        else if (i > 0 && matrixEntries[i - 1].Line > line - 8 &&
                                 matrixEntries[i - 1].Line < line && matrixEntries[i].Line > line)
                        {
                            entry = matrixEntries[i - 1];
                        }
                    }
                }
                if (entry != null)
                {
                    DW2StateMachine actualEntry = entry.Value;
                    result = actualEntry.Address;
                    return true;
                }
                return false;
            });
            return result;
        }

        public class DW2DebugLineHeader
        {
            public DW2DebugLineHeader(EndianBinaryReader er)
            {
                Length = er.ReadUInt32();
                Version = er.ReadUInt16();
                PrologueLength = er.ReadUInt32();
                MinInstructionLength = er.ReadByte();
                DefaultIsStatement = er.ReadByte() == 1;
                LineBase = er.ReadSByte();
                LineRange = er.ReadByte();
                OpcodeBase = er.ReadByte();
                StandardOpcodeLengths = er.ReadBytes(OpcodeBase - 1);
            }

            public uint Length;
            public ushort Version;
            public uint PrologueLength;
            public ushort MinInstructionLength;
            public bool DefaultIsStatement;
            public sbyte LineBase;
            public byte LineRange;
            public byte OpcodeBase;
            public byte[] StandardOpcodeLengths;
        }

        public class DW2FileEntry
        {
            public DW2FileEntry(EndianBinaryReader er)
            {
                FileName = er.ReadStringNT(Encoding.ASCII);
                DirectoryIndex = IOUtil.ReadUnsignedLeb128(er);
                LastModified = IOUtil.ReadUnsignedLeb128(er);
                FileSize = IOUtil.ReadUnsignedLeb128(er);
            }

            public string FileName;
            public uint DirectoryIndex;
            public uint LastModified;
            public uint FileSize;
        }

        private struct DW2StateMachine
        {
            public DW2StateMachine(bool isStmt)
            {
                Address = 0;
                File = 1;
                Line = 1;
                Column = 0;
                IsStmt = isStmt;
                BasicBlock = false;
                EndSequence = false;
            }

            public uint Address;
            public uint File;
            public uint Line;
            public uint Column;
            public bool IsStmt;
            public bool BasicBlock;
            public bool EndSequence;
        }

        public struct AddressLookupResult
        {
            public uint Address;
            public string Directory;
            public string File;
            public int Line;
            public int Column;
        }
    }
}
