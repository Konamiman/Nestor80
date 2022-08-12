using System;

namespace Konamiman.Nestor80
{
    internal class Program
    {
        static void Main(string[] args)
        {
            /*
            var g = new RelFileGenerator();
            g.AddLinkItem(LinkItemType.ProgramName, "EINS");
            g.AddLinkItem(LinkItemType.SizeOfDataArea, AddressType.ASEG, 0);
            g.AddLinkItem(LinkItemType.ProgramSize, AddressType.CSEG, 3);
            //g.AddLinkItem(LinkItemType.SetLocationCounter, AddressType.CSEG, 0x200);
            g.AddAbsoluteBytes(new byte[] { 0x11,0x22,0x33 });
            g.AddLinkItem(LinkItemType.DefineEntryPoint, AddressType.ASEG, 0x2233, "HOLA");
            g.AddLinkItem(LinkItemType.EndProgram, AddressType.ASEG, 0);
            g.AddLinkItem(LinkItemType.EndFile);
            var bytes = g.GetBytes();
            var extraLength = 128 - bytes.Length;
            if(extraLength > 0) {
                bytes = bytes.Concat(new byte[extraLength]).ToArray();
            }
            File.WriteAllBytes(@"L:\home\konamiman\Nestor80\EINS.REL", bytes.ToArray());
            //return;
            */

            //Write();
            Read();
        }

        //Special link item 'H':
        //Expects a file named like B plus .REL, text file having a number and a label,
        //the number is the start address of data area (which is placed anyway before program area),
        //the label is for I don't know what.

        static void Write()
        {
            var g = new RelFileGenerator();

            /*
            g.AddLinkItem(LinkItemType.ProgramName, "Y");
            g.AddExtensionLinkItem(Convert.ToByte('H'), AddressType.ASEG, 0, "EINS");
            g.AddLinkItem(LinkItemType.SizeOfDataArea, AddressType.ASEG, 0);
            g.AddLinkItem(LinkItemType.ProgramSize, AddressType.CSEG, 3);
            g.AddAbsoluteBytes(new byte[] { 0x21 });
            g.AddLinkItem(LinkItemType.ExternalMinusOffset, AddressType.ASEG, 1);
            g.AddAbsoluteBytes(new byte[] { 0, 0 });
            g.AddLinkItem(LinkItemType.ChainExternal, AddressType.CSEG, 1, "FOO");
            g.AddLinkItem(LinkItemType.EndProgram, AddressType.CSEG, 0x210);
            g.AddLinkItem(LinkItemType.EndFile);
            */

            g.AddLinkItem(LinkItemType.ProgramName, "Y");
            g.AddExtensionLinkItem(Convert.ToByte('H'), AddressType.CSEG, 0x200, "EINS");
            g.AddLinkItem(LinkItemType.DataAreaSize, AddressType.ASEG, 2);
            g.AddLinkItem(LinkItemType.ProgramAreaSize, AddressType.CSEG, 10);
            g.AddAbsoluteBytes(new byte[] { 1, 2, 3, 4, 0, 0 });
            g.AddAddress(AddressType.CSEG, 4);
            g.AddAddress(AddressType.CSEG, 6);
            g.AddLinkItem(LinkItemType.SetLocationCounter, AddressType.DSEG, 0);
            g.AddAbsoluteBytes(new byte[] { 0xAA, 0xBB });
            g.AddLinkItem(LinkItemType.ChainExternal, AddressType.CSEG, 8, "FOO");
            g.AddLinkItem(LinkItemType.EndProgram, AddressType.ASEG, 0x120);
            g.AddLinkItem(LinkItemType.EndFile);


            var bytes = g.GetBytes();
            var extraLength = 128 - bytes.Length;
            if(extraLength > 0) {
                bytes = bytes.Concat(new byte[extraLength]).ToArray();
            }

            File.WriteAllBytes(@"L:\home\konamiman\Nestor80\Y2.REL", bytes.ToArray());
            return;

            g.AddAbsoluteBytes(new byte[] { 1, 3, 5 });
            g.AddLinkItem(LinkItemType.ProgramName, "FOO");
            g.AddLinkItem(LinkItemType.RequestLibrarySearch, new byte[] { 0x41, 0x42, 0x43 });
            g.AddLinkItem(LinkItemType.DefineEntryPoint, AddressType.CSEG, 0x1234, "BAR");
            g.AddLinkItem(LinkItemType.ExternalPlusOffset, AddressType.DSEG, 0xABCD);
            //g.AddExtensionLinkItem(0x41, 1, 0x5577, "FIZZ");
            g.AddLinkItem(LinkItemType.EndFile);
            var x = g.GetBytes();

            var p = new RelFileParser(x);
            p.ParseFile();
        }

        //L80 /P:1100,Y,YEXT,YEXT/N/E/Y/X
        //objcopy -I ihex -O binary YEXT.HEX YEXT.BIN

        static void Read()
        {
            // var bytes = File.ReadAllBytes(@"L:\home\konamiman\Nextor\source\kernel\bank0\DOSHEAD.REL");
            var bytes = File.ReadAllBytes(@"L:\home\konamiman\Nestor80\EXPR.REL");
            //var bytes = File.ReadAllBytes(@"C:\code\fun\MSX\SRC\SDCC\char\printf_simple.rel");
            var parser = new RelFileParser(bytes);
            parser.ParseFile();
        }
    }
}