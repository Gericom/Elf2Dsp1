using System;

namespace Elf2Dsp1.ELF.Sections
{
    public class ElfSection
    {
        public ELF.SectionHeaderTableEntry SectionHeader { get; }
        public String Name { get; }

        protected ElfSection(ELF.SectionHeaderTableEntry sectionHeader, string name)
        {
            SectionHeader = sectionHeader;
            Name = name;
        }

        public static ElfSection CreateInstance(ELF.SectionHeaderTableEntry section, string name)
        {
            switch (section.SectionType)
            {
                case ELF.SectionHeaderTableEntry.ElfSectionType.Strtab:
                    return new ElfStrtab(section, name);
                case ELF.SectionHeaderTableEntry.ElfSectionType.Symtab:
                    return new ElfSymtab(section, name);
                default:
                    return new ElfSection(section, name);
            }
        }

        public override string ToString()
        {
            return Name;
        }
    }
}
