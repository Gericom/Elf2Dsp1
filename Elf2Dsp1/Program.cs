using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Elf2Dsp1
{
    class Program
    {
        static void Main(string[] args)
        {
            string outputPath = null;
            string inputPath  = null;
            bool   syncLoad   = false;
            for (int i = 0; i < args.Length; i++)
            {
                if (i == args.Length - 1)
                    inputPath = args[i];
                else
                {
                    switch (args[i])
                    {
                        case "-o":
                            outputPath = args[++i];
                            break;
                        case "-s":
                            syncLoad = true;
                            break;
                        default:
                            Console.WriteLine("Error while parsing arguments!");
                            return;
                    }
                }
            }

            if (inputPath == null)
            {
                Console.WriteLine("Error while parsing arguments!");
                return;
            }

            if (outputPath == null)
                outputPath = Path.ChangeExtension(inputPath, ".cdc");

            var elf  = new ELF.ELF(File.ReadAllBytes(inputPath));
            var dsp1 = new DSP1();
            if (syncLoad)
                dsp1.Header.Flags |= DSP1.DSP1Header.DSP1Flags.SyncLoad;
           
            var segments = new List<DSP1.DSP1Segment>();
            var textSection = elf.GetSectionByName(".text");
            if (textSection.SectionHeader.SectionData != null && textSection.SectionHeader.SectionData.Length > 0)
            {
                var segment = new DSP1.DSP1Segment();
                segment.SegmentType = DSP1.SegmentType.Prog0;
                segment.SegmentData = textSection.SectionHeader.SectionData ?? new byte[0];
                segment.Address     = (textSection.SectionHeader.VirtualAddress & 0xFFFFFF) >> 1;
                segments.Add(segment);
            }          

            var rodataSection = elf.GetSectionByName(".rodata");
            if (rodataSection.SectionHeader.SectionData != null && rodataSection.SectionHeader.SectionData.Length > 0)
            {
                var segment = new DSP1.DSP1Segment();
                segment.SegmentType = DSP1.SegmentType.Data;
                segment.SegmentData = rodataSection.SectionHeader.SectionData ?? new byte[0];
                segment.Address     = (rodataSection.SectionHeader.VirtualAddress & 0xFFFFFF) >> 1;
                segments.Add(segment);
            }

            var dataSection = elf.GetSectionByName(".data");
            if (dataSection.SectionHeader.SectionData != null && dataSection.SectionHeader.SectionData.Length > 0)
            {
                var segment = new DSP1.DSP1Segment();
                segment.SegmentType = DSP1.SegmentType.Data;
                segment.SegmentData = dataSection.SectionHeader.SectionData ?? new byte[0];
                segment.Address     = (dataSection.SectionHeader.VirtualAddress & 0xFFFFFF) >> 1;
                segments.Add(segment);
            }

            dsp1.Segments = segments.ToArray();

            var result = dsp1.Write();
            File.Create(outputPath).Close();
            File.WriteAllBytes(outputPath, result);
        }
    }
}