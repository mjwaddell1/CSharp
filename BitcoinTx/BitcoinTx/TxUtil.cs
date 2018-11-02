using System;
using System.Linq;
using System.Threading;
using NBitcoin.Protocol;
using NBitcoin;
using System.Collections.Generic;

namespace BitcoinTx
{
	public class TxUtil
	{

		//generate a random extended private key here (BIP32 Extended Private Key):
		//https://coinomi.com/recovery-phrase-tool.html

		public static string ElectrumXhost = "";
		public static int ElectrumXport = 0;
		public static string BTChost = "";
		public static int BTCport = 0;

		public static long EstimateFee()
		{
			return ElectrumX.EstimateFee(20, ElectrumXhost, ElectrumXport);
		}

		//get child keys (addresses) for given extended private key
		public static string[] GetDerivedKeysPvt(string extPrivateKey, uint start, uint cnt, bool testnet)
		{
			CheckNullOrEmpty(new object[] { extPrivateKey }, new string[] { "extPrivateKey" });

			Network ntwk = testnet ? Network.TestNet : Network.Main;
			ExtKey k = ExtKey.Parse(extPrivateKey);
			ExtPubKey pk = k.Neuter(); //get public ext key
			string[] res = new string[cnt];
			for (uint ctr = start; ctr < cnt; ctr++)
				//return Private and Public key pair
				res[ctr] = k.Derive(ctr).PrivateKey.ToString(ntwk) + ":" + pk.Derive(ctr).PubKey.GetAddress(ntwk);
			return res;
		}

		//get child keys for given extended public key
		public static List<Tuple<string, string>> GetDerivedKeys(string extPublicKey, uint start, uint cnt, bool chg, bool testnet) //public
		{
			CheckNullOrEmpty(new object[] { extPublicKey }, new string[] { "extPublicKey" });

			//key path  -44'\1'\0'\0   << public addresses (0' is wallet [hardened])
			//key path  -44'\1'\0'\1   << change addresses

			//https://programmingblockchain.gitbooks.io/programmingblockchain/content/key_generation/key_generation.html#hd-wallet-bip-32

			Network ntwk = testnet ? Network.TestNet : Network.Main;
			ExtPubKey k = ExtPubKey.Parse(extPublicKey); //xpub6BszcyR5c6gyrgchEk3XUrdFv4YfWEQzPXJBDN9WE7BEP5mwuSMnbuBv2khudobtUdKwLv1yvRACexYKgStbyKEPcKFTtQzQNdvy61rfLLC
			List<Tuple<string, string>> lst = new List<Tuple<string, string>>();
			uint addrType = chg ? 1u : 0u; //if change address, type=1. Else type=0 (receive)
			for (uint ctr = start; ctr < start + cnt; ctr++)
				//return short address and long address needed by nbitcoin transaction
				lst.Add(new Tuple<string, string>(
					k.Derive(addrType).Derive(ctr).PubKey.GetAddress(ntwk).ToString(), //short
					k.Derive(addrType).Derive(ctr).PubKey.ToString()));  //long
			return lst;
		}

		//scan derived addresses for any balance
		public static long GetExtendedBalance(string extPubKey, uint start, uint cnt, uint stopAfter, bool testnet)
		{
			CheckNullOrEmpty(new object[] { extPubKey }, new string[] { "extPubKey" });

			List<Tuple<string, string>> recAddrList = GetDerivedKeys(extPubKey, start, cnt, false, testnet); //receive addresses
			List<Tuple<string, string>> chgAddrList = GetDerivedKeys(extPubKey, start, cnt, true, testnet); //change addresses

			string[] recAddrListAddr = new string[recAddrList.Count]; //short address - myB9vrgbz4THVf....
			string[] recAddrListExt = new string[recAddrList.Count]; //extended address - tpubD6NzVbkrYh.....

			int ctr = 0;
			foreach (var t in recAddrList)
				recAddrListAddr[ctr++] = t.Item1; //short - address
			ctr = 0;
			foreach (var t in recAddrList)
				recAddrListExt[ctr++] = t.Item2; //long - public key

			string[] chgAddrListAddr = new string[recAddrList.Count];
			string[] chgAddrListExt = new string[recAddrList.Count];

			ctr = 0;
			foreach (var t in chgAddrList)
				chgAddrListAddr[ctr++] = t.Item1;
			ctr = 0;
			foreach (var t in chgAddrList)
				chgAddrListExt[ctr++] = t.Item2;

			long bal = 0;
			//add up all UTXOs (unspent inputs) in address list
			MainWindow.MainWin.UpdateStatus(">>>> Receive Addresses");
			bal += ElectrumX.GetBalance(recAddrListAddr, stopAfter, ElectrumXhost, ElectrumXport);
			MainWindow.MainWin.UpdateStatus(">>>> Change Addresses");
			bal += ElectrumX.GetBalance(chgAddrListAddr, stopAfter, ElectrumXhost, ElectrumXport);
			return bal;
		}

		//get all unspent inputs for extended public key
		public static UTXO[] GetExtendedUTXOs(string extPublicKey, uint start, uint maxcnt, bool testnet)
		{
			CheckNullOrEmpty(new object[] { extPublicKey }, new string[] { "extPublicKey" });

			//get child keys for extended public key
			List<Tuple<string, string>> keysRec = GetDerivedKeys(extPublicKey, start, maxcnt, false, testnet); //receive addresses
			List<Tuple<string, string>> keysChg = GetDerivedKeys(extPublicKey, start, maxcnt, true, testnet); //change addresses
			List<UTXO> lst = new List<UTXO>();
			//use ElectrumX server to get all unspent inputs
			lst.AddRange(ElectrumX.GetUTXOs(keysRec, ElectrumXhost, ElectrumXport));
			lst.AddRange(ElectrumX.GetUTXOs(keysChg, ElectrumXhost, ElectrumXport));
			//we actually used maxcnt twice (for receiving addresses and change addresses), so may need to truncate
			if (lst.Count > maxcnt)
				lst.Resize((int)maxcnt);
			return lst.ToArray();
		}

		public static string CreateTxJSON(string extPubKey, string pubToAddr, string chgAddr, int walletId, long satToSend, long fee, bool testnet)
		{
			TxSerial ts = CreateTx(extPubKey, pubToAddr, chgAddr, walletId, satToSend, fee, testnet);
			return Newtonsoft.Json.JsonConvert.SerializeObject(ts, Newtonsoft.Json.Formatting.Indented);
		}

		//take in tx parameters, return tx object
		public static TxSerial CreateTx(string extPubKey, string pubToAddr, string chgAddr, int walletId, long satToSend, long fee, bool testnet)
		{
			CheckNullOrEmpty(new object[] { extPubKey, pubToAddr, ElectrumXhost },
				new string[] { "extPubKey", "pubToAddr", "ElectrumXhost" });

			string err = "";

			if (satToSend == 0) err += "satoshiToSend = 0, ";
			if (fee == 0) err += "satoshiFee = 0, ";
			if (err != "") throw new Exception("[CreateTx] " + err);

			//get first 100+100 child address from ext pub key
			List<Tuple<string, string>> recAddrList = GetDerivedKeys(extPubKey, 0, 20, false, testnet); //receive addresses
			List<Tuple<string, string>> chgAddrList = GetDerivedKeys(extPubKey, 0, 20, true, testnet); //change addresses

			//TODO - create process for getting next change address, so address never used twice
			if (chgAddr == null || chgAddr == "") //get first chg addr for extPubKey
				chgAddr = chgAddrList.First().Item1;

			//server status check 
			string info = ElectrumX.GetServerInfo(ElectrumXhost, ElectrumXport);
			if (info == null)
				throw new Exception("[CreateTx] ElectrumX Server Check Failed");

			string[] recAddrListAddr = new string[recAddrList.Count]; //short address
			string[] recAddrListExt = new string[recAddrList.Count]; //long address
			int ctr = 0;
			foreach (var t in recAddrList)
				recAddrListAddr[ctr++] = t.Item1; //short
			ctr = 0;
			foreach (var t in recAddrList)
				recAddrListExt[ctr++] = t.Item2; //long - hash

			string[] chgAddrListAddr = new string[recAddrList.Count];
			string[] chgAddrListExt = new string[recAddrList.Count];
			ctr = 0;
			foreach (var t in chgAddrList)
				chgAddrListAddr[ctr++] = t.Item1;
			ctr = 0;
			foreach (var t in chgAddrList)
				chgAddrListExt[ctr++] = t.Item2;

			//get all UTXOs (unspent inputs) from receive addresses
			UTXO[] recUTXOs = ElectrumX.GetUTXOs(recAddrList, ElectrumXhost, ElectrumXport);
			UTXO.SetAddressType(recUTXOs, 0); //receiver
											  //get all UTXOs (unspent inputs) from change addresses
			UTXO[] chgUTXOs = ElectrumX.GetUTXOs(chgAddrList, ElectrumXhost, ElectrumXport);
			UTXO.SetAddressType(chgUTXOs, 1); //change

			//start new tx
			TransactionBuilder bldr = new TransactionBuilder();
			bldr.Send(new BitcoinPubKeyAddress(pubToAddr), Money.Satoshis(satToSend)); //amount to send to recipient
			bldr.SetChange(new BitcoinPubKeyAddress(chgAddr)); //send change to this address
			bldr.SendFees(Money.Satoshis(fee)); //miner (tx) fee 

			//collect all UTXOs
			List<UTXO> allUTXOs = new List<UTXO>();
			allUTXOs.AddRange(recUTXOs);
			allUTXOs.AddRange(chgUTXOs);

			List<ICoin> lstTxCoins = new List<ICoin>(); //Coin is a UTXO

			//add new coin for each UTXO
			foreach (UTXO x in allUTXOs) //tx builder will select coins from this list
			{
				BitcoinPubKeyAddress fromAddr = new BitcoinPubKeyAddress(x.Address);
				NBitcoin.Coin cn = null;
				//create new coin from UTXO
				bldr.AddCoins(cn = new NBitcoin.Coin(
					new OutPoint(new uint256(x.Tx_hash), x.Tx_pos),  //tx that funded wallet, spend this coin
					new TxOut(Money.Satoshis(x.Value), fromAddr.ScriptPubKey)));   //specify full coin amount, else SetChange ignored
				lstTxCoins.Add(cn); //add coin to transaction, may not be used
				x.tmp = cn; //link UTXO with coin
			}

			List<UTXO> usedUTXOs = new List<UTXO>(); //coins actually used in tx
			NBitcoin.Transaction tx = bldr.BuildTransaction(false); //sort\filter coins, some coins will not be needed\used
			//coin objects not stored in tx, so we need to determine which coins were used
			//scan tx inputs for matching coins, ignore other coins
			foreach (UTXO u in allUTXOs)
				foreach (TxIn i in tx.Inputs)
					if (i.PrevOut == ((NBitcoin.Coin)u.tmp).Outpoint) //this coin in tx
						usedUTXOs.Add(u); //this UTXO will be used\spent in tx

			//populate return object
			TxSerial txs = new TxSerial()
			{
				SendAmt = satToSend,
				Fee = fee,
				ExtPublicKey = extPubKey,
				ToAddress = pubToAddr,
				ChgAddress = chgAddr,
				WalletId = walletId
			};
			txs.ExtPublicKey = extPubKey;

			foreach (UTXO u in usedUTXOs)
				u.tmp = null; //don't serialize coin object, will rebuild coins in signing process
			txs.InputUTXOs = new List<UTXO>();
			txs.InputUTXOs.AddRange(usedUTXOs);
			//string jsn = Newtonsoft.Json.JsonConvert.SerializeObject(txs, Newtonsoft.Json.Formatting.Indented);
			return txs;
		}

		public static string SignTx(string TxJSON, string extPrivateKey, bool testnet)
		{
			try
			{
				Network ntwk = testnet ? Network.TestNet : Network.Main;
				TxSerial txs = Newtonsoft.Json.JsonConvert.DeserializeObject<TxSerial>(TxJSON);
				ExtKey p2 = ExtKey.Parse(extPrivateKey);
				ExtKey RootKey = p2;

				TransactionBuilder bldr = new TransactionBuilder();
				bldr.Send(new BitcoinPubKeyAddress(txs.ToAddress), Money.Satoshis(txs.SendAmt));
				bldr.SetChange(new BitcoinPubKeyAddress(txs.ChgAddress));
				bldr.SendFees(Money.Satoshis(txs.Fee));

				//re-add coins
				List<Coin> lstCoin = new List<Coin>();
				foreach (UTXO u in txs.InputUTXOs)
				{
					Coin cn;
					bldr.AddCoins(cn = new Coin(
						new OutPoint(new uint256(u.Tx_hash), u.Tx_pos),  //tx that funded wallet, spend this coin
						new TxOut(Money.Satoshis(u.Value), new BitcoinPubKeyAddress(u.Address))));
					lstCoin.Add(cn);
				}
				Transaction tx = bldr.BuildTransaction(false); //will select coins

				foreach (UTXO u in txs.InputUTXOs)
				{
					Coin cn;
					bldr.AddCoins(cn = new Coin(
						new OutPoint(new uint256(u.Tx_hash), u.Tx_pos),  //tx that funded wallet, spend this coin
						new TxOut(Money.Satoshis(u.Value), new BitcoinPubKeyAddress(u.Address))));
					//check if coin in spend list, else error  (should already be done in CreateTx)
					foreach (TxIn i in tx.Inputs)
						if (i.PrevOut == cn.Outpoint) //this coin is in tx
							//sign utxo with corresponding private key
							bldr.AddKnownSignature(new PubKey(u.PublicKey), tx.SignInput(new BitcoinSecret(RootKey.Derive((uint)u.AddrType).Derive(u.AddrIndex).PrivateKey, ntwk), cn));
				}

				Transaction txSigned = bldr.BuildTransaction(true);
				NBitcoin.Policy.TransactionPolicyError[] pe;
				bool verify = bldr.Verify(txSigned, out pe);

				string hx = txSigned.ToHex();
				return hx;
			}
			catch (Exception ex)
			{
				string result = "ERROR: " + ex.Message;
				return result;
			}
		}

		//send signed tx hex to bitcoin network
		public static string BroadcastTx(string SignedTxHex, bool testnet)
		{
			CheckNullOrEmpty(new object[] { SignedTxHex, BTChost }, new string[] { "SignedTxHex", "BTChost" });

			NBitcoin.Transaction tx = NBitcoin.Transaction.Parse(SignedTxHex);
			Network ntwk = testnet ? Network.TestNet : Network.Main;
			string err = "";

			var nd = Node.Connect(ntwk, BTChost + ":" + BTCport); //DACC bitcoin server (BitcoinD)
																  //bitcoin node sends responses asynchronously
			nd.MessageReceived += (node, message) =>
				{
					NBitcoin.Protocol.IncomingMessage msgx = message;
					if (msgx.Message.Payload is RejectPayload) //error message
					{
						RejectPayload py = (RejectPayload)msgx.Message.Payload;
						Console.WriteLine("Rejected:" + py.Message + " " + py.Reason);
						err += py.Message + " " + py.Reason;
					}
				};
			nd.VersionHandshake(); //must send node version first
			Thread.Sleep(1000);
			nd.SendMessage(new InvPayload(tx));
			Thread.Sleep(1000);
			nd.SendMessage(new TxPayload(tx));
			Thread.Sleep(5000); //wait for any error msgs
			string msg = tx.GetHash().ToString();
			try
			{
				bool broadcasted = false;
				//search mempool for transaction
				foreach (var txid in nd.GetMempool()) //throws error for some servers
				{
					if (txid.Equals(tx.GetHash()))
						broadcasted = true;
				}
				nd.Disconnect();
				//return json message
				if (!broadcasted)
					msg = "Broadcast Failed (tx hash = " + tx.GetHash().ToString() + ")" + err + "\"}";
				else
					msg = tx.GetHash().ToString();
			}
			catch (Exception ex) { msg = "Error (BroadcastTx): " + ex.Message; }
			return msg;
		}

		public static void CheckNullOrEmpty(object[] objList, string[] objName) //for checking methods params
		{
			for (int i = 0; i < objList.Length; i++)
			{
				if (objList[i] == null)
				{
					//calling method is frame 1
					string meth = new System.Diagnostics.StackTrace().GetFrame(1).GetMethod().Name;
					throw new NullReferenceException("[" + meth + "] " + objName[i] + " is null");
				}
				else
					if (objList[i] is string && string.IsNullOrEmpty((string)objList[i]))
				{
					//calling method is frame 1
					string meth = new System.Diagnostics.StackTrace().GetFrame(1).GetMethod().Name;
					throw new NullReferenceException("[" + meth + "] " + objName[i] + " is empty");
				}
			}
		}

		public static string GetDerivedKeysAll(string extKey, uint cnt, bool testnet) //can pass in ext private or public key
		{
			try
			{
				string result = "";
				Network ntwk = testnet ? Network.TestNet : Network.Main;

				bool IsPrv = extKey.Substring(1, 3) == "prv"; //xprv...
				ExtPubKey epbk;
				if (IsPrv)
					epbk = ExtKey.Parse(extKey).Neuter();
				else
					epbk = ExtPubKey.Parse(extKey);

				result += "\n" + (IsPrv ? "Private" : "Public") + " key entered. Testnet=" + (ntwk == Network.TestNet) + "\n";
				bool chg;
				if (IsPrv) //show derived private keys
				{
					ExtKey k = ExtKey.Parse(extKey);
					result += "\n" + "Ext Private Key: " + k.ToString(ntwk);
					result += "\n" + "-- Derived Private Keys --";
					chg = false;
					result += "\n" + " >> Recipient Addresses";
					for (uint ctr = 0; ctr < cnt; ctr++)
						result += "\n" + "" + ctr + ") " + k.Derive(chg ? 1u : 0u).Derive(ctr).PrivateKey.ToString(ntwk) + " : " + k.Derive(chg ? 1u : 0u).Derive(ctr).ToString(ntwk);
					result += "\n" + " >> Change Addresses";
					chg = true;
					for (uint ctr = 0; ctr < cnt; ctr++)
						result += "\n" + "" + ctr + ") " + k.Derive(chg ? 1u : 0u).Derive(ctr).PrivateKey.ToString(ntwk) + " : " + k.Derive(chg ? 1u : 0u).Derive(ctr).ToString(ntwk);
					result += "\n";
				}
				result += "\n" + "Ext Public Key: " + epbk.ToString(ntwk);
				result += "\n" + "-- Derived Public Keys --";
				chg = false;
				result += "\n" + " >> Recipient Addresses";
				for (uint ctr = 0; ctr < cnt; ctr++)
					result += "\n" + "" + ctr + ") " + epbk.Derive(chg ? 1u : 0u).Derive(ctr).PubKey.GetAddress(ntwk) + " : " + epbk.Derive(chg ? 1u : 0u).Derive(ctr).ToString(ntwk);
				result += "\n" + " >> Change Addresses";
				chg = true;
				for (uint ctr = 0; ctr < cnt; ctr++)
					result += "\n" + "" + ctr + ") " + epbk.Derive(chg ? 1u : 0u).Derive(ctr).PubKey.GetAddress(ntwk) + " : " + epbk.Derive(chg ? 1u : 0u).Derive(ctr).ToString(ntwk);

				return result;
			}
			catch (Exception ex)
			{
				return "Error: " + ex.Message;
			}
		}
	}
}