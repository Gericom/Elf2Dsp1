namespace Elf2Dsp1.ELF.Sections
{
    public class ElfStrtab : ElfSection
    {
        public ElfStrtab(ELF.SectionHeaderTableEntry section, string name)
            : base(section, name)
        { }

        public string GetString(uint offset)
        {
            string cur = "";
            while (offset < SectionHeader.SectionData.Length)
            {
                char c = (char)SectionHeader.SectionData[offset++];
                if (c == '\0')
                    return cur;
                cur += c;
            }
            return null;
        }
    }
}
