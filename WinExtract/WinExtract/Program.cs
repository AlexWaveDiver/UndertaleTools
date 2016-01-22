﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Drawing;

namespace WinExtract
{
    class Program
    {   
        static BinaryReader bread;
        static BinaryWriter bwrite;
        static string input_folder;
        static uint chunk_limit;        
        static uint FONT_offset;
        static uint FONT_limit;
        static uint STRG_offset;        

        struct endFiles
        {
            public string name;
            public uint offset;
            public uint size;            
        }

        struct spriteInfo
        {
            public uint x;
            public uint y;
            public uint w;
            public uint h;
            public uint i;
        }

        static void Main(string[] args)
        {
            if (args.Length != 2)
            {
                System.Console.WriteLine("Usage: winextract <output .win file> <input folder>");
                return;
            }
            string output_win = args[0];
            input_folder = args[1];
            if (input_folder[input_folder.Length - 1] != '\\') input_folder += '\\';
            uint full_size = (uint)new FileInfo(output_win).Length;
            bread = new BinaryReader(File.Open(output_win, FileMode.Open));
            Directory.CreateDirectory(input_folder + "CHUNK");

            uint chunk_offset = 0;

            while (chunk_offset < full_size)
            {
                string chunk_name = new String(bread.ReadChars(4));
                uint chunk_size = bread.ReadUInt32();
                chunk_offset = (uint)bread.BaseStream.Position;
                chunk_limit = chunk_offset + chunk_size;
                System.Console.WriteLine("Chunk "+chunk_name+" offset:"+chunk_offset+" size:"+chunk_size);

                List<endFiles> filesToCreate = new List<endFiles>();

                if (chunk_name == "FORM")
                {
                    full_size = chunk_limit;
                    chunk_size = 0;
                }
                else if (chunk_name == "TPAG")
                {
                    //StreamWriter tpag = new StreamWriter(input_folder + "TPAG.txt", false, System.Text.Encoding.ASCII);
                    //uint sprite_count = bread.ReadUInt32();
                    //bread.BaseStream.Position += sprite_count * 4;//Skip offsets
                    //for (uint i = 0; i < sprite_count; i++)
                    //{
                    //    tpag.Write(bread.ReadInt16());//x
                    //    tpag.Write(";");
                    //    tpag.Write(bread.ReadInt16());//y
                    //    tpag.Write(";");
                    //    tpag.Write(bread.ReadInt16());//w1
                    //    tpag.Write(";");
                    //    tpag.Write(bread.ReadInt16());//h1
                    //    tpag.Write(";");
                    //    tpag.Write(bread.ReadInt16());//?
                    //    tpag.Write(";");
                    //    tpag.Write(bread.ReadInt16());//?
                    //    tpag.Write(";");
                    //    tpag.Write(bread.ReadInt16());//w2
                    //    tpag.Write(";");                        
                    //    tpag.Write(bread.ReadInt16());//h2
                    //    tpag.Write(";");
                    //    tpag.Write(bread.ReadInt16());//w3
                    //    tpag.Write(";");
                    //    tpag.Write(bread.ReadInt16());//h3
                    //    tpag.Write(";");
                    //    tpag.Write(bread.ReadInt16());//txtr id
                    //    tpag.Write((char)0x0D);
                    //    tpag.Write((char)0x0A);
                    //}
                    //bread.BaseStream.Position = chunk_offset;
                }
                else if (chunk_name == "STRG")
                {
                    STRG_offset = (uint)bread.BaseStream.Position;
                    bwrite = new BinaryWriter(File.Open(input_folder + "STRG.txt", FileMode.Create));
                    uint strings = bread.ReadUInt32();
                    bread.BaseStream.Position += strings * 4;//Skip offsets
                    for (uint i = 0; i < strings; i++)
                    {
                        uint string_size = bread.ReadUInt32()+1;                         
                        for (uint j = 0; j < string_size; j++)
                            bwrite.Write(bread.ReadByte());
                        bwrite.BaseStream.Position--;
                        bwrite.Write((byte)0x0D);
                        bwrite.Write((byte)0x0A);
                    }
                    long bacp = bread.BaseStream.Position;                    
                    recordFiles(collectFonts(input_folder), "FONT");
                    bread.BaseStream.Position = bacp;
                    filesToCreate.Clear();                    
                }
                else if (chunk_name == "TXTR")
                {
                    List<uint> entries = collect_entries(false);
                    for (int i = 0; i < entries.Count-1; i++)
                    {
                        uint offset = entries[i];
                        bread.BaseStream.Position = offset + 4;
                        offset = bread.ReadUInt32();
                        entries[i] = offset;
                    }
                    filesToCreate = new List<endFiles>();
                    for (int i = 0; i < entries.Count - 1; i++)
                    {
                        uint offset = entries[i];
                        uint next_offset = entries[i+1];
                        uint size = next_offset - offset;
                        endFiles f1 = new endFiles();
                        f1.name = "" + i + ".png";
                        f1.offset = offset;
                        f1.size = size;
                        filesToCreate.Add(f1);
                    }
                }
                else if (chunk_name == "AUDO")
                {
                    List<uint> entries = collect_entries(false);
                    filesToCreate = new List<endFiles>();
                    for (int i = 0; i < entries.Count - 1; i++)
                    {
                        uint offset = entries[i];
                        bread.BaseStream.Position = offset;
                        uint size = bread.ReadUInt32();
                        offset = (uint)bread.BaseStream.Position;
                        endFiles f1 = new endFiles();
                        f1.name = "" + i + ".wav";
                        f1.offset = offset;
                        f1.size = size;
                        filesToCreate.Add(f1);
                    }
                }
                else if(chunk_name == "FONT") {
                    FONT_offset = (uint)bread.BaseStream.Position;
                    FONT_limit = chunk_limit;
                }

                if (chunk_name != "FORM")
                if (filesToCreate.Count==0)
                {                    
                    string name = "CHUNK//" + chunk_name + ".chunk";
                    uint bu = (uint)bread.BaseStream.Position;
                    bread.BaseStream.Position = chunk_offset;
                    
                    bwrite = new BinaryWriter(File.Open(input_folder+name, FileMode.Create));
                    for (uint i=0;i<chunk_size;i++)
                        bwrite.Write(bread.ReadByte());
                    bread.BaseStream.Position = bu;
                }
                else 
                {
                    recordFiles(filesToCreate, chunk_name);                    
                    
                    if (chunk_name == "TXTR") collectFontImages();
                }

                chunk_offset += chunk_size;
                bread.BaseStream.Position = chunk_offset;
            }

            File.Open(input_folder + "translate.txt", FileMode.OpenOrCreate);
            Directory.CreateDirectory(input_folder + "patch");
            File.Open(input_folder + "patch\\patch.txt", FileMode.OpenOrCreate);
        }           

        static List<uint> collect_entries(bool fnt)
        {
            List<uint> entries = new List<uint>();
            uint files = bread.ReadUInt32();
            for(uint i = 0; i < files; i++)
            {
                uint offset = bread.ReadUInt32();
                if (offset != 0)
                {
                    entries.Add(offset);
                }
            }
            entries.Add(fnt ? FONT_limit : chunk_limit);            
            return entries;
        }

        static void recordFiles(List<endFiles> files, string folder) {            
            Directory.CreateDirectory(input_folder + folder);
            for (int i = 0; i < files.Count; i++)
            {
                string name = files[i].name;
                uint bu = (uint)bread.BaseStream.Position;
                bread.BaseStream.Position = files[i].offset;

                bwrite = new BinaryWriter(File.Open(input_folder + folder + "\\" + name, FileMode.Create));
                for (uint j = 0; j < files[i].size; j++)
                    bwrite.Write(bread.ReadByte());
                bwrite.Close();
                bread.BaseStream.Position = bu;
            }            
        }

        static List<endFiles> collectFonts(string input_folder) {
            bread.BaseStream.Position = FONT_offset;
            List<uint> entries = collect_entries(true);
            List<endFiles> filesToCreate = new List<endFiles>();
            for (int i = 0; i < entries.Count-1; i++)
            {
                uint offset = entries[i];
                bread.BaseStream.Position = offset;                                
                endFiles f1 = new endFiles();
                string font_name = getSTRGEntry(bread.ReadUInt32());                
                string font_family = getSTRGEntry(bread.ReadUInt32());
                f1.name = ""+i+"_"+font_name+" ("+font_family+")";
                f1.offset = offset;
                f1.size = calculateFontSize(offset);
                filesToCreate.Add(f1);
            }
            return filesToCreate;
        }

        static uint calculateFontSize(uint font_offset) {
            uint result = 44;
            long bacup = bread.BaseStream.Position;

            bread.BaseStream.Position = font_offset+40;
            uint glyphs = bread.ReadUInt32();
            result +=  glyphs * 20;

            bread.BaseStream.Position = bacup;
            return result;
        }

        static void collectFontImages() {
            long bacup = bread.BaseStream.Position;
            bread.BaseStream.Position = FONT_offset;
            List<uint> fonts = collect_entries(false);
            for (int f=0; f<fonts.Count-1; f++)
            {
                bread.BaseStream.Position = fonts[f]+28;
                spriteInfo sprt = getSpriteInfo(bread.ReadUInt32());
                Bitmap texture = new Bitmap(Image.FromFile(input_folder+"TXTR\\"+sprt.i+".png"));
                Bitmap cropped = texture.Clone(new Rectangle((int)sprt.x, (int)sprt.y, (int)sprt.w, (int)sprt.h), texture.PixelFormat);
                cropped.Save(input_folder + "FONT\\" + f + ".png");
            }

            bread.BaseStream.Position = bacup;
        }

        static spriteInfo getSpriteInfo(uint sprite_offset)
        {
            spriteInfo result = new spriteInfo();
            long bacup = bread.BaseStream.Position;
            bread.BaseStream.Position = sprite_offset;
            result.x = bread.ReadUInt16();
            result.y = bread.ReadUInt16();
            result.w = bread.ReadUInt16();
            result.h = bread.ReadUInt16();
            bread.BaseStream.Position += 12;
            result.i = bread.ReadUInt16();
            bread.BaseStream.Position = bacup;
            return result;
        }

        static string getSTRGEntry(uint str_offset)
        {
            long bacup = bread.BaseStream.Position;            
            bread.BaseStream.Position = str_offset-4;//???
            byte[] strar = new byte[bread.ReadInt32()];
            for (int f = 0; f < strar.Length; f++)
                strar[f] = bread.ReadByte();
            
            bread.BaseStream.Position = bacup;
            return System.Text.Encoding.ASCII.GetString(strar);//UTF-8?
        }
    }
}
