using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitcoinTx
{
	public class TxSerial
	{
		public long SendAmt; //satoshi
		public long Fee; //satoshi
		public string ExtPublicKey; //tpubDDKxGAxcyYakRdW....
		public string ToAddress; //mhZudKag...
		public string ChgAddress; //mpR3tvX...
		public int WalletId; //0, 1, 2 ....
		public List<UTXO> InputUTXOs;
		public string Error;
	}

	public class UTXO : IComparable
	{
		public string Tx_hash; //prev tx hash
		public int Tx_pos;
		public int Height;
		public int Value; //satoshi
		public string Address; //mpR3tvX5otiLs93MTVSsdoPaXXDyJVe5Zg
		public string PublicKey; //needed for signing - 0205e8946bceece1e2ed1121c370653c68f844890dcd26c2214f942098197ce687
		public int AddrType; //receive=0\change=1 44'/1'/0'/#
		public uint AddrIndex; //44'/1'/0'/#/#
		public object tmp;

		int IComparable.CompareTo(object obj) //sortable by value
		{
			UTXO x = (UTXO)obj;
			if (this.Value == x.Value) return 0;
			return this.Value > x.Value ? 1 : -1;
		}

		public static UTXO[] SetAddressType(UTXO[] lst, int addressType) //set receiver or change address
		{
			foreach (UTXO u in lst)
				u.AddrType = addressType;
			return lst;
		}
	}

}
