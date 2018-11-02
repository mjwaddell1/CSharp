using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Text;
using System.Net.Sockets;
using System.Collections.Generic;

namespace BitcoinTx
{
	public class ElectrumX
	{
		//get status of ElectrumX server
		public static string GetServerInfo(string server, int port) //check if server running
		{
			string resp = null;
			using (var socket = new TcpClient(server, port)) //("18.221.223.44",50001))
			{
				String json = "{\"id\": \"1\", \"method\": \"server.version\", \"params\": [\"3.0.3\", \"1.1\"], \"jsonrpc\" : \"1.0\"}\n";
				var body = Encoding.UTF8.GetBytes(json);
				using (var stream = socket.GetStream())
				{
					stream.Write(body, 0, body.Length);
					byte[] bb = new byte[10000];
					int k = stream.Read(bb, 0, 10000);
					resp = Encoding.UTF8.GetString(bb, 0, k);
					if (resp.Contains("error") && !resp.Replace(" ", "").Contains("error\":null"))
						throw new Exception("[GetServerInfo] ERROR:" + resp);
					else
						return resp;
				}
			}
		}

		//get all unspent inputs (UTXOs) from address array
		public static UTXO[] GetUTXOs(string[] addresses, string server, int port)
		{
			List<UTXO> lst = new List<UTXO>();
			foreach (string addr in addresses)
				lst.AddRange(GetUTXOs(addr, server, port));
			UTXO[] ret = lst.ToArray();
			Array.Sort(ret);
			return ret;
		}

		//get all unspent inputs (UTXOs) from single address
		public static UTXO[] GetUTXOs(string address, string server, int port)
		{
			using (var socket = new TcpClient(server, port)) //("18.221.223.44",50001))
			{
				String json = "{\"id\": \"1\", \"method\": \"blockchain.address.listunspent\", \"params\": [\"" + address + "\"], \"jsonrpc\" : \"1.0\"}\n";
				var body = Encoding.UTF8.GetBytes(json);
				using (var stream = socket.GetStream())
				{
					stream.Write(body, 0, body.Length);

					byte[] bb = new byte[15000]; //should be enough
					int k = stream.Read(bb, 0, 15000);
					if (k == 15000) //very large reply
					{
						Array.Resize(ref bb, 100000); //definitely enough
						k += stream.Read(bb, 15000, 100000 - 15000); //finish msg
						if (k == 100000) //problem if reply this large
							throw new Exception("[GetUTXOs] ERROR:Msg reply too large");
					}
					string resp = Encoding.UTF8.GetString(bb, 0, k);
					if (resp.Contains("error") && !resp.Replace(" ", "").Contains("error\":null"))
						throw new Exception("[GetUTXOs] ERROR:" + resp);
					else
						return ParseUTXO(resp); //parse json
				}
			}
		}

		//get all unspent inputs (UTXOs) from address list
		public static UTXO[] GetUTXOs(List<Tuple<string, string>> addresses, string server, int port)
		{
			List<UTXO> lst = new List<UTXO>();
			uint ctr = 0;
			foreach (var a in addresses)
			{
				UTXO[] xx = GetUTXOs(a.Item1, server, port);
				foreach (UTXO x in xx)
				{
					x.Address = a.Item1; //short address - mpjmGT77hP4ZiGQLbT6B6N3cYe3C3zP7Af
					x.PublicKey = a.Item2; //pub key hash - 0205e8946bceece1e2ed1121c370653c68f844890dcd26c2214f942098197ce687
					x.AddrIndex = ctr;
				}
				ctr++;
				lst.AddRange(xx);
			}

			UTXO[] ret = lst.ToArray();
			Array.Sort(ret);
			return ret;
		}

		//parse json UTXO response
		public static UTXO[] ParseUTXO(string msg)
		{
			List<UTXO> lst = new List<UTXO>();
			/*
            {"jsonrpc": "2.0", "id": "1", "result": 
            [{"tx_hash": "60c21462d1ddbac55ed34f205b9776303953c2b74de1f4283630e69db78d65ec", "tx_pos": 1, "height": 1260557, "value": 129967000}, 
            {"tx_hash": "04fa60e351ffedff984e7cad9697a532dd6dc97966ed64b26919cc1741639861", "tx_pos": 0, "height": 1261826, "value": 16230000}, 
            {"tx_hash": "70c3f2ff2721846bac77bf8aad863efdbc20ed0cc2f59dda6d7dfd02682c2805", "tx_pos": 0, "height": 1261827, "value": 32480000}]
            } 
            */
			msg = msg.Replace("\"", "").Replace(" ", "");
			/*
            {jsonrpc:2.0,id:1,result: 
            [{tx_hash:60c21462d1ddbac55ed34f205b9776303953c2b74de1f4283630e69db78d65ec,tx_pos:1,height:1260557,value:129967000}, 
            {tx_hash:04fa60e351ffedff984e7cad9697a532dd6dc97966ed64b26919cc1741639861,tx_pos:0,height:1261826,value:16230000}, 
            {tx_hash:70c3f2ff2721846bac77bf8aad863efdbc20ed0cc2f59dda6d7dfd02682c2805,tx_pos:0,height:1261827,value:32480000}]
            } 
            */
			int pos = 0;
			while (true)
			{
				pos = msg.IndexOf("tx_hash", pos);
				if (pos < 0) break;
				int pos2 = msg.IndexOf("}", pos) - 1;
				string oneutxo = msg.Substring(pos, pos2 - pos + 1); //tx_hash:60c21462d1ddbac55ed34f205b9776303953c2b74de1f4283630e69db78d65ec,tx_pos:1,height:1260557,value:129967000
				string[] vals = oneutxo.Split(',');
				UTXO x = new UTXO();
				x.Tx_hash = vals[0].Split(':')[1];           //tx_hash:60c21462d1ddbac55ed34f205b9776303953c2b74de1f4283630e69db78d65ec
				x.Tx_pos = int.Parse(vals[1].Split(':')[1]); //tx_pos:1
				x.Height = int.Parse(vals[2].Split(':')[1]); //height:1260557
				x.Value = int.Parse(vals[3].Split(':')[1]);  //value:129967000 (satoshi)
				lst.Add(x);
				pos = pos2;
			}
			UTXO[] arr = lst.ToArray();
			Array.Sort(arr); //smallest value first
			return arr;
		}

		//get only UTXOs with enough total bitcoin to cover tx
		public static UTXO[] GetUTXOsForTx(UTXO[] xlst, long sendAmt)
		{
			//NBitcoin does its own coin selection. This method not currently used.

			//for now, just start with smallest, add to total
			Array.Sort(xlst);
			List<UTXO> xout = new List<UTXO>();
			long ttl = 0;
			for (int i = 0; i < xlst.Length; i++)
			{
				xout.Add(xlst[i]);
				ttl += xlst[i].Value; //satoshi
				if (ttl >= sendAmt) //enough for Tx?
					return xout.ToArray();
			}
			//problem, not enough funds
			return new UTXO[0]; //empty array
		}

		//sum all UTXOs to get total balance for address array
		public static long GetBalance(string[] addresses, uint stopAfter, string server, int port)
		{
			//sum balances for all addresses - ElectrumX
			long bal = 0;
			long b = 0;
			int zerocnt = 0; //if x keys have zero balance, stop checking
			foreach (String addr in addresses)
			{
				bal += (b = GetBalance(addr, server, port));
				if (b > 0) zerocnt = 0;
				if (++zerocnt > stopAfter)
					break;
				MainWindow.MainWin.UpdateStatus(addr + " : " + b);
			}
			return bal;
		}

		//sum all UTXOs to get balance for single address
		public static long GetBalance(string address, string server, int port)
		{
			//balance for single address - ElectrumX
			using (var socket = new TcpClient(server, port)) //("18.221.223.44",50001))
			{
				String json = "{\"id\": \"1\", \"method\": \"blockchain.address.get_balance\", \"params\": [\"" + address + "\"], \"jsonrpc\" : \"1.0\"}\n";
				var body = Encoding.UTF8.GetBytes(json);
				using (var stream = socket.GetStream())
				{
					stream.Write(body, 0, body.Length);

					byte[] bb = new byte[10000];
					int k = stream.Read(bb, 0, 10000);
					string resp = Encoding.UTF8.GetString(bb, 0, k);
					if (resp.Contains("error") && !resp.Replace(" ", "").Contains("error\":null"))
						throw new Exception("ERROR:" + resp);
					else
					{
						//{"jsonrpc": "2.0", "id": "1", "result": {"confirmed": 0, "unconfirmed": 0}}
						int pos = resp.IndexOf("confirmed") + 11;
						int pos2 = resp.IndexOf(',', pos);
						string qq = resp.Substring(pos, pos2 - pos);
						long q = long.Parse(qq);
						return q;
					}
				}
			}
		}

		//estimate tx fee based on previous # blocks
		public static long EstimateFee(int blocks, string server = "electrum.akinbo.org", int port = 50001)
		{
			//18.221.223.44             50001   tcp  //dacc
			//testnet.qtornado.com	    51002	ssl
			//testnet.hsmiths.com	    53012	ssl
			//hsmithsxurybd7uh.onion	53011	tcp
			//testnetnode.arihanc.com	51001	tcp
			//electrum.akinbo.org	    51001	tcp
			//electrum.akinbo.org	    51002	ssl
			//using (var socket = new TcpClient("testnetnode.arihanc.com",50001))
			using (var socket = new TcpClient(server, port)) //("18.221.223.44",50001))
			{
				String json = "{\"id\": \"1\", \"method\": \"blockchain.estimatefee\", \"params\": [\"" + blocks + "\"], \"jsonrpc\" : \"1.0\"}\n";
				var body = Encoding.UTF8.GetBytes(json);
				using (var stream = socket.GetStream())
				{
					stream.Write(body, 0, body.Length);

					byte[] bb = new byte[10000];
					int k = stream.Read(bb, 0, 10000);
					string resp = Encoding.UTF8.GetString(bb, 0, k);
					if (resp.Contains("error") && !resp.Replace(" ","").Contains("error\":null"))
						throw new Exception("ERROR:" + resp);
					else
					{
						resp = resp.Replace('}', ',');
						//"{\"result\": 1e-05, \"error\": null, \"id\": \"1\"}\n"
						int pos = resp.IndexOf("result") + 9; //{"jsonrpc": "2.0", "id": "1", "result": 1.015e-05}
						int pos2 = resp.IndexOf(',',pos); //{"jsonrpc": "2.0", "id": "1", "result": 1.015e-05}
						string qq = resp.Substring(pos, pos2 - pos); // 1.015e-05
						decimal q = (decimal)double.Parse(qq);
						return (long)(q * 100000000); //satoshis
					}
				}
			}
		}
	}
}