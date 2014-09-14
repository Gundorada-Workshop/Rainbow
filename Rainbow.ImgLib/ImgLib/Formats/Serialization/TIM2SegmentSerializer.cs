﻿//Copyright (C) 2014 Marco (Phoenix) Calautti.

//This program is free software: you can redistribute it and/or modify
//it under the terms of the GNU General Public License as published by
//the Free Software Foundation, version 2.0.

//This program is distributed in the hope that it will be useful,
//but WITHOUT ANY WARRANTY; without even the implied warranty of
//MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//GNU General Public License 2.0 for more details.

//A copy of the GPL 2.0 should have been included with the program.
//If not, see http://www.gnu.org/licenses/

//Official repository and contact information can be found at
//http://github.com/marco-calautti/Rainbow

using Rainbow.ImgLib.Encoding;
using Rainbow.ImgLib.Formats.Serialization;
using Rainbow.ImgLib.Formats.Serialization.Metadata;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace Rainbow.ImgLib.Formats.Serialization
{
    internal class TIM2SegmentSerializer : TextureFormatSerializer
    {
        private bool swizzled;

        public TIM2SegmentSerializer()
        {
        }

        public TIM2SegmentSerializer(bool swizzled)
        {
            this.swizzled = swizzled;
        }

        public string Name { get { return TIM2Segment.NAME; } }

        public string PreferredFormatExtension { get { return ""; } }

        //public string PreferredMetadataExtension{ get {return ""; } }

        public bool IsValidFormat(Stream input)
        {
            throw new NotImplementedException();
        }

        
        public bool IsValidMetadataFormat(MetadataReader metadata)
        {
            throw new NotImplementedException();
        }

        public TextureFormat Open(Stream formatData)
        {

            uint dataSize, paletteSize, colorEntries;
            TIM2Segment.TIM2SegmentParameters parameters;

            AcquireInfoFromHeader(formatData, out parameters, out dataSize, out paletteSize, out colorEntries);

            byte[] imageData = new byte[dataSize];
            formatData.Read(imageData, 0, (int)dataSize);

            byte[] paletteData = new byte[paletteSize];
            formatData.Read(paletteData, 0, (int)paletteSize);

            return new TIM2Segment(imageData, paletteData, colorEntries, parameters);
        }

        public void Save(TextureFormat texture, Stream outFormatData)
        {
            TIM2Segment segment = texture as TIM2Segment;
            if (segment == null)
                throw new TextureFormatException("Not A valid TIM2Segment!");

            byte[] imageData = segment.GetImageData();
            byte[] paletteData = segment.GetPaletteData();
            TIM2Segment.TIM2SegmentParameters parameters = segment.GetParameters();

            //write header
            WriteHeader(parameters, outFormatData, imageData, paletteData);
            outFormatData.Write(imageData, 0, imageData.Length);
            outFormatData.Write(paletteData, 0, paletteData.Length);
        }

        public void Export(TextureFormat texture, MetadataWriter metadata, string directory, string basename)
        {
            TIM2Segment segment = texture as TIM2Segment;
            if (segment == null)
                throw new TextureFormatException("Not A valid TIM2Segment!");

            Writemetadata(segment, metadata, basename);
            int i = 0;
            foreach (Image img in ConstructImages(segment))
            {
                img.Save(Path.Combine(directory, basename + "_" + i++ + ".png"));
            }
        }

        public TextureFormat Import(MetadataReader metadata, string directory, string bname)
        {
            TIM2Segment segment = null;

            int palCount;
            string basename;

            TIM2Segment.TIM2SegmentParameters parameters;
            Readmetadata(metadata, out parameters, out basename, out palCount);
            ICollection<Image> images = ReadImageData(directory, basename, palCount);

            segment = new TIM2Segment(images, parameters);

            return segment;
        }

        private ICollection<Image> ConstructImages(TIM2Segment segment)
        {

            var list = new List<Image>();
            int oldSelected = segment.SelectedPalette;
            for (int i = 0; i < (segment.PalettesCount == 0 ? 1 : segment.PalettesCount); i++)
            {
                segment.SelectedPalette = i;
                list.Add(segment.GetImage());
            }
            segment.SelectedPalette = oldSelected;
            return list;
        }

        private ICollection<Image> ReadImageData(string directory, string basename, int palCount)
        {

            ICollection<Image> images = new List<Image>();
            for (int i = 0; i < (palCount == 0 ? 1 : palCount); i++)
            {
                string file = Path.Combine(directory, basename + "_" + i + ".png");
                images.Add(Image.FromFile(file));
            }

            return images;
        }

        private void Readmetadata(MetadataReader metadata, out TIM2Segment.TIM2SegmentParameters parameters, out string basename, out int palCount)
        {

            metadata.EnterSection("TIM2Texture");

            basename = metadata.GetAttributeString("Basename");
            palCount = metadata.GetAttributeInt("Cluts");

            parameters = new TIM2Segment.TIM2SegmentParameters();

            parameters.swizzled = swizzled;

            parameters.linearPalette = metadata.GetAttributeBool("LinearClut");

            parameters.width = metadata.GetInt("Width");
            parameters.height = metadata.GetInt("Height");
            parameters.bpp = (byte)metadata.GetInt("Bpp");
            parameters.colorSize = metadata.GetInt("ColorSize");
            parameters.mipmapCount = (byte)metadata.GetInt("MipmapCount");

            parameters.format = (byte)metadata.GetInt("Format");
            //parameters.clutFormat = (byte)int.Parse(node.Element("ClutFormat").Value);

            parameters.GsTEX0 = metadata.GetRaw("GsTEX0");
            parameters.GsTEX1 = metadata.GetRaw("GsTEX1");

            parameters.GsRegs = (uint)metadata.GetInt("GsRegs");
            parameters.GsTexClut = (uint)metadata.GetInt("GsTexClut");

            parameters.userdata = metadata.GetRaw("UserData");

            metadata.ExitSection();

        }

        private void Writemetadata(TIM2Segment segment, MetadataWriter metadata, string basename)
        {

            metadata.BeginSection("TIM2Texture");
            metadata.PutAttribute("Basename", basename);
            metadata.PutAttribute("Cluts", segment.PalettesCount);
            metadata.PutAttribute("LinearClut", segment.GetParameters().linearPalette);

            metadata.Put("Width", segment.GetParameters().width);
            metadata.Put("Height", segment.GetParameters().height);
            metadata.Put("Bpp", segment.GetParameters().bpp);
            metadata.Put("ColorSize", segment.GetParameters().colorSize);
            metadata.Put("MipmapCount", segment.GetParameters().mipmapCount);

            //xml.WriteComment("Raw data from TIM2 header");
            metadata.Put("Format", segment.GetParameters().format);
            //xml.WriteElementString("ClutFormat", segment.GetParameters().clutFormat.ToString());
            metadata.Put("GsTEX0", segment.GetParameters().GsTEX0);
            metadata.Put("GsTEX1", segment.GetParameters().GsTEX1);

            metadata.Put("GsRegs", (int)segment.GetParameters().GsRegs);
            metadata.Put("GsTexClut", (int)segment.GetParameters().GsTexClut);
            metadata.Put("UserData", segment.GetParameters().userdata);

            metadata.EndSection();
        }

        private void WriteHeader(TIM2Segment.TIM2SegmentParameters parameters, Stream outFormatData, byte[] imageData, byte[] paletteData)
        {
            BinaryWriter writer = new BinaryWriter(outFormatData);
            uint totalSize = (uint)(0x30 + parameters.userdata.Length + imageData.Length + paletteData.Length);
            writer.Write(totalSize);
            writer.Write((uint)paletteData.Length);
            writer.Write((uint)imageData.Length);
            writer.Write((ushort)(0x30 + parameters.userdata.Length));

            ushort colorEntries = (ushort)(paletteData.Length / parameters.colorSize);
            writer.Write(colorEntries);

            writer.Write(parameters.format);
            writer.Write(parameters.mipmapCount);

            byte clutFormat = (byte)(parameters.bpp > 8 ? 0 : parameters.colorSize - 1);

            clutFormat |= parameters.linearPalette ? (byte)0x80 : (byte)0;

            writer.Write(clutFormat);
            byte depth;
            switch (parameters.bpp)
            {
                case 4:
                    depth = 4;
                    break;
                case 8:
                    depth = 5;
                    break;
                case 16:
                    depth = 1;
                    break;
                case 24:
                    depth = 2;
                    break;
                case 32:
                    depth = 3;
                    break;
                default:
                    throw new Exception("Should never happen");
            }
            writer.Write(depth);
            writer.Write((ushort)parameters.width);
            writer.Write((ushort)parameters.height);
            writer.Write(parameters.GsTEX0);
            writer.Write(parameters.GsTEX1);
            writer.Write(parameters.GsRegs);
            writer.Write(parameters.GsTexClut);
            writer.Write(parameters.userdata);
        }

        private void AcquireInfoFromHeader(Stream formatData, out TIM2Segment.TIM2SegmentParameters parameters, out uint dataSize, out uint paletteSize, out uint colorEntries)
        {
            byte[] fullHeader = new byte[0x30];
            formatData.Read(fullHeader, 0, fullHeader.Length);

            BinaryReader reader = new BinaryReader(new MemoryStream(fullHeader));

            uint totalSize = reader.ReadUInt32();
            paletteSize = reader.ReadUInt32();
            dataSize = reader.ReadUInt32();
            ushort headerSize = reader.ReadUInt16();

            int userDataSize = headerSize - 0x30;

            colorEntries = reader.ReadUInt16();

            parameters = new TIM2Segment.TIM2SegmentParameters();
            parameters.swizzled = swizzled;

            parameters.format = reader.ReadByte();

            parameters.mipmapCount = reader.ReadByte();

            if (parameters.mipmapCount > 1)
                throw new TextureFormatException("Mipmapped images not supported yet!");

            byte clutFormat = reader.ReadByte();

            byte depth = reader.ReadByte();

            switch (depth)
            {
                case 01:
                    parameters.bpp = 16;
                    break;
                case 02:
                    parameters.bpp = 24;
                    break;
                case 03:
                    parameters.bpp = 32;
                    break;
                case 04:
                    parameters.bpp = 4;
                    break;
                case 05:
                    parameters.bpp = 8;
                    break;
                default:
                    throw new TextureFormatException("Illegal bit depth!");
            }

            parameters.width = reader.ReadUInt16();
            parameters.height = reader.ReadUInt16();

            parameters.GsTEX0 = reader.ReadBytes(8);
            parameters.GsTEX1 = reader.ReadBytes(8);

            parameters.GsRegs = reader.ReadUInt32();
            parameters.GsTexClut = reader.ReadUInt32();

            reader.Close();

            parameters.linearPalette = (clutFormat & 0x80) != 0;
            clutFormat &= 0x7F;

            parameters.colorSize = parameters.bpp > 8 ? parameters.bpp / 8 : clutFormat + 1;

            if (userDataSize > 0)
            {
                byte[] data = new byte[userDataSize];
                formatData.Read(data, 0, userDataSize);
                parameters.userdata = data;
            }

        }

    }
}
