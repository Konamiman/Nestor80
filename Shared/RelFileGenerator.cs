using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Konamiman.Nestor80
{
    public class RelFileGenerator
    {
        private readonly List<byte> buffer = new();
        private readonly BitStreamWriter bsw;

        public RelFileGenerator()
        {
            bsw = new BitStreamWriter(buffer);
        }

        public void AddAbsoluteBytes(byte[] bytes)
        {
            foreach(var b in bytes ) {
                bsw.Write(0, 1);
                bsw.Write(b, 8);
            }
        }

        public void AddAddress(AddressType addressType, uint value)
        {
            bsw.Write(1, 1);
            AddAddressCore(addressType, value);
        }

        private void AddAddressCore(AddressType addressType, uint value)
        {
            bsw.Write((byte)addressType, 2);
            bsw.Write(value & 0xFF, 8);
            bsw.Write(value >> 8, 8);
        }

        public void AddLinkItem(LinkItemType type)
        {
            AddLinkItemCore(type, null, null, null);
        }


        public void AddLinkItem(LinkItemType type, AddressType addressType, uint address)
        {
            AddLinkItemCore(type, addressType, address, null);
        }

        public void AddLinkItem(LinkItemType type, byte[] B)
        {
            AddLinkItemCore(type, null, null, B);
        }

        public void AddLinkItem(LinkItemType type, string B)
        {
            AddLinkItemCore(type, null, null, Encoding.ASCII.GetBytes(B));
        }

        public void AddLinkItem(LinkItemType type, AddressType addressType, uint address, byte[] B)
        {
            AddLinkItemCore(type, addressType, address, B);
        }

        public void AddLinkItem(LinkItemType type, AddressType addressType, uint address, string B)
        {
            AddLinkItemCore(type, addressType, address, Encoding.ASCII.GetBytes(B));
        }

        public void AddExtensionLinkItem(byte type, string B)
        {
            AddExtensionLinkItem(type, Encoding.ASCII.GetBytes(B));
        }

        public void AddExtensionLinkItem(byte type, byte[] B)
        {
            var bytes = new byte[] { type }.Concat(B).ToArray();
            AddLinkItemCore((LinkItemType)4, null, null, bytes);
        }

        private void AddLinkItemCore(LinkItemType type, AddressType? addressType, uint? address, byte[] B)
        {
            bsw.Write(1, 1);
            bsw.Write(0, 2);
            bsw.Write((byte)type, 4);
            if(addressType != null) {
                AddAddressCore(addressType.Value, address.Value);
            }
            if(B!=null) {
                bsw.Write((byte)B.Length, 3);
                foreach(byte b in B) {
                    bsw.Write(b, 8);
                }
            }

            if(type == LinkItemType.EndProgram) {
                bsw.ForceByteBoundary();
            }
        }


        public byte[] GetBytes()
        {
            return buffer.ToArray();
        }
    }
}
