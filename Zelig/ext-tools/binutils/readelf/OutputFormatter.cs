﻿using System;
using System.Text;
using System.Runtime.InteropServices;
using Microsoft.binutils.elflib;

namespace Microsoft.binutils.readelf
{
    class OutputFormatter
    {
        public static string PrintElfHeader(Elf32_Ehdr hdr)
        {
            var sb = new StringBuilder();

            sb.AppendLine("ELF Header:");

            // -- print Ident section e.g. "Magic Numbers" -- //

            var raw    = new byte[Marshal.SizeOf(hdr.e_ident)];
            var handle = GCHandle.Alloc(raw, GCHandleType.Pinned);
            var ptr    = handle.AddrOfPinnedObject();

            Marshal.StructureToPtr(hdr.e_ident, ptr, false);

            handle.Free();

            sb.AppendLine("Magic:  " + BitConverter.ToString(raw));

            // --------------------------------------------- //

            var val  = (EI_CLASS) hdr.e_ident.EI_CLASS;
            var val2 = (EI_DATA) hdr.e_ident.EI_DATA;
            var val3 = (e_version) hdr.e_ident.EI_VERSION;
            var val4 = (EI_OSABI) hdr.e_ident.EI_OSABI;
            var val5 = (e_type) hdr.e_type;
            var val6 = (e_machine) hdr.e_machine;
            
            sb.AppendLine("Class:                             " + val);
            sb.AppendLine("Data:                              " + val2);          
            sb.AppendLine("Version:                           " + val3);            
            sb.AppendLine("OS/ABI:                            " + val4);
            sb.AppendLine("ABI Version:                       " + hdr.e_ident.EI_ABIVERSION);           
            sb.AppendLine("Type:                              " + val5);
            sb.AppendLine("Machine:                           " + val6);
            sb.AppendLine("Version:                           " + "0x" + hdr.e_version.ToString("X"));
            sb.AppendLine("Entry point address:               " + "0x" + hdr.e_entry.ToString("X"));
            sb.AppendLine("Start of program headers:          " + hdr.e_phoff + " (bytes into file)");
            sb.AppendLine("Start of section headers:          " + hdr.e_shoff + " (bytes into file)");
            sb.AppendLine("Flags:                             " + "0x" + hdr.e_flags.ToString("X"));
            sb.AppendLine("Size of this header:               " + hdr.e_ehsize + " (bytes)");
            sb.AppendLine("Size of program headers:           " + hdr.e_phentsize + " (bytes)");
            sb.AppendLine("Number of program headers:         " + hdr.e_phnum);
            sb.AppendLine("Size of section hdeaders:          " + hdr.e_shentsize + " (bytes)");
            sb.AppendLine("Number of section headers:         " + hdr.e_shnum);
            sb.AppendLine("Section header string table index: " + hdr.e_shtrndx);

            return sb.ToString();
        }

        public static string PrintSectionHeaders(ElfSection[] sections)
        {
            var sb = new StringBuilder();

            sb.AppendLine("Section Headers:");

            sb.AppendLine("[Nr] Name                  Type            Addr     Off    Size   ES Flg Lk Inf Al");

            for(int i = 0; i < sections.Length; i++)
            {
                var shdr = sections[i].Header;

                sb.Append("[" + i.ToString("D2") + "] ");
                sb.Append(sections[i].Name.PadRight(22));
                sb.Append(((sh_type) shdr.sh_type).ToString().PadRight(16));
                sb.Append(shdr.sh_addr.ToString("X8").PadRight(9));
                sb.Append(shdr.sh_offset.ToString("X6").PadRight(7));
                sb.Append(shdr.sh_size.ToString("X6").PadRight(7));
                sb.Append(shdr.sh_entsize.ToString("X2").PadRight(3));

                string displayFormat = "WAXMSILOGxop";
                string output        = "";
                uint   flags         = (uint)shdr.sh_flags;
                
                int x = 0;

                while(x < 9)
                {
                    if ((flags & 1) > 0)
                    {
                        output += displayFormat.Substring(x, 1);
                    }

                    if(x == 2)
                    {
                        flags >>= 2;
                    }
                    else if(x == 8)
                    {
                        flags >>= 4;
                    }
                    else
                    {
                        flags >>= 1;
                    }

                    x++;
                }

                if((shdr.sh_flags & sh_flags.SHF_MASKOS) > 0)
                {
                    output += "o";
                }

                if((shdr.sh_flags & sh_flags.SHF_MASKPROC) > 0)
                {
                    output += "p";
                }

                sb.Append(output.PadLeft(3).PadRight(4));
                sb.Append(shdr.sh_link.ToString("D2").PadRight(3));
                sb.Append(shdr.sh_info.ToString("D2").PadLeft(3));
                sb.Append(shdr.sh_addralign.ToString("X1").PadLeft(3));

                sb.AppendLine();
            }

            sb.AppendLine("Key to Flags:");
            sb.AppendLine("W (write), A (alloc), X (execute), M (merge), S (strings)");
            sb.AppendLine("I (info), L (link order), G (group), x (unknown)");
            sb.AppendLine("0 (extra OS processing required), o (OS specific), p (processor specific)");

            return sb.ToString();
        }

        public static string PrintSymbolTable(SymbolTable tbl)
        {
            var sb = new StringBuilder();

            sb.AppendLine("Symbol table '" + tbl.Name + "' containts " + tbl.Symbols.Length + " entries:");

            sb.AppendLine("   Num:    Value  Size Type    Bind   Vis      Ndx Name");

            for (UInt16 i = 0; i < tbl.Symbols.Length; i++)
            {
                var symbol = tbl[i];

                sb.Append((i + ": ").PadLeft(8));
                sb.Append(symbol.SymbolDef.st_value.ToString("X8").PadRight(9));
                sb.Append(symbol.SymbolDef.st_size.ToString().PadLeft(5).PadRight(6));
                sb.Append(symbol.Type.ToString().Substring(symbol.Type.ToString().IndexOf("_") + 1).PadRight(8));
                sb.Append(symbol.Binding.ToString().Substring(symbol.Binding.ToString().IndexOf("_") + 1).PadRight(7));
                sb.Append(symbol.Visibility.ToString().Substring(symbol.Visibility.ToString().IndexOf("_") + 1).PadRight(9));

                if(symbol.SymbolDef.st_shndx == (ushort)NamedIndexes.SHN_UNDEF)
                {
                    sb.Append("UND".PadRight(4));
                }
                else if(symbol.SymbolDef.st_shndx == (ushort)NamedIndexes.SHN_ABS)
                {
                    sb.Append("ABS".PadRight(4));
                }
                else
                {
                    sb.Append(symbol.SymbolDef.st_shndx.ToString().PadLeft(3).PadRight(4));
                }

                sb.Append(symbol.Name);

                sb.AppendLine();
            }

            return sb.ToString();
        }

        public static string PrintProgramHeaders(ElfObject elf)
        {
            var sb   = new StringBuilder();

            var ehdr = elf.Header;

            sb.AppendLine("Elf file type is " + ((e_type)ehdr.e_type));
            sb.AppendLine("Entry point 0x" + ehdr.e_entry.ToString("X8"));
            sb.AppendLine("There are " + ehdr.e_phnum + " program headers, starting at offset " + ehdr.e_phoff);
            sb.AppendLine();
            sb.AppendLine("Program headers:");
            sb.AppendLine("   Type           Offset   VirtAddr   PhysAddr   FileSiz MemSiz  Flg Align");

            var segments = elf.Segments;

            foreach (var segment in segments)
            {
                sb.Append("   " + ((SegmentType) segment.Header.p_type).ToString().PadRight(15));
                sb.Append("0x" + segment.Header.p_offset.ToString("X6") + " ");
                sb.Append("0x" + segment.Header.p_vaddr.ToString("X8") + " ");
                sb.Append("0x" + segment.Header.p_paddr.ToString("X8") + " ");
                sb.Append("0x" + segment.Header.p_filesz.ToString("X5") + " ");
                sb.Append("0x" + segment.Header.p_memsz.ToString("X5") + " ");

                var flag  = ((segment.Header.p_flags & SegmentFlag.PF_R) != 0) ? "R" : " ";
                    flag += ((segment.Header.p_flags & SegmentFlag.PF_W) != 0) ? "W" : " ";
                    flag += ((segment.Header.p_flags & SegmentFlag.PF_X) != 0) ? "E" : " ";

                sb.Append(flag + " ");
                sb.Append("0x" + segment.Header.p_align.ToString("X"));
                sb.AppendLine();
            }

            sb.AppendLine();
            sb.AppendLine(" Section to Segment mapping:");
            sb.AppendLine("  Segment Sections...");

            for (int i = 0; i < segments.Length; i++)
            {
                sb.Append("   " + i.ToString("D2") + "     ");

                foreach (var section in segments[i].ReferencedSections)
                {
                    sb.Append(section.Name + " ");
                }

                sb.AppendLine();
            }
            
            return sb.ToString();
        }

        public static string PrintRelocationEntries(RelocationSection[] sections)
        {
            var sb = new StringBuilder();

            foreach (var section in sections)
            {
                sb.AppendLine("Relocation section '" + section.Name + "' at offset 0x" +
                              section.Header.sh_offset.ToString("X3") + " contains " + section.Entries.Length +
                              " entries:");

                sb.AppendLine(" Offset     Info    Type            Sym.Value  Sym.Name");

                foreach (var entry in section.Entries)
                {
                    sb.Append(entry.EntryDef.r_offset.ToString("X8").PadRight(10));
                    sb.Append(entry.EntryDef.r_info.ToString("X8").PadRight(9));
                    sb.Append(entry.Type.ToString().PadRight(17));
                    sb.Append(entry.ReferencedSymbol.SymbolDef.st_value.ToString("X8").PadRight(11));

                    if (entry.ReferencedSymbol.Name != "")
                    {
                        sb.Append(entry.ReferencedSymbol.Name);
                    }
                    else
                    {
                        sb.Append(entry.ReferencedSymbol.ReferencedSection.Name);
                    }

                    sb.AppendLine();
                }

                sb.AppendLine();
            }

            return sb.ToString();
        }
    }
}
