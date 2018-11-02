using System;
using System.Windows;
using System.Threading.Tasks;
using NBitcoin;

namespace BitcoinTx
{
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window
	{
		public static MainWindow MainWin = null;

		public bool IsTestNet = true; //if false, use MainNet

		public MainWindow()
		{
			//n2bb698rcD8WNGCje3J6pJDYhT2VfTzQyD  0.55
			InitializeComponent();
			txtExtPvtKey.Text = "tprv8j2PWZc5LeVdQWSA58MjB8ZBLQM3xDMFufv26kZ3wUcKeSR419MG6LJ6QQsTodNJ2fWU1NsodWsRMZ8KAPJkSbaKXK3vEdkSn2Qo1v7FnxV";
			txtExtPubKey.Text = "tpubDFiReyeKV2BJHyTwxn2KaYDHuRrz7YYAUyWoPGbMMkQiUvfpdYArGpuxaZPYCmRmNyFhg62sX6EPripSo6wG5hbGSerKTAmuN9z6c1vEbcF";
			string pvtKeyAddr = "cUxutv9UwUuRJdRtrfRwSXFCWQFNE7qX5Z41TG4DGxu6rP6XH59i"; //2nd derived addr
			string pubSendAddr = "mzpzv4n9UmaAjC7gBJmqktfQQwrDRmWT5Z"; //2nd derived addr
			txtRecAddr.Text = "mhAmxzttQ6H91NRHZfg6iY6LgiGTRzdeGr "; //3rd derived rec address
			txtChgAddr.Text = "mx4iJ5roJwiDnpe6cE1GUSd8P4NTBhUh1f  "; //1st derived chg address
			txtSendAmt.Text = "5432"; //satoshis
			txtFeeAmt.Text = "2000"; //satoshis
			MainWin = this;

			//set servers
			if (IsTestNet)
			{
				TxUtil.ElectrumXhost = "testnet.qtornado.com";
				TxUtil.ElectrumXport = 51001; //50001
				TxUtil.BTChost = "tbtc.blockr.io";
				TxUtil.BTCport = 18333;
			}
			else //mainnet
			{
				TxUtil.ElectrumXhost = "ndndword5lpb7eex.onion";
				TxUtil.ElectrumXport = 50001;
				TxUtil.BTChost = "ndndword5lpb7eex.onion";
				TxUtil.BTCport = 8333;
			}
		}

		public void CreateAddresses() //use this to create your own keys\addresses
		{
			//create random ext key
			ExtKey pvtKey = new ExtKey();
			string extPrvKey = (pvtKey).GetWif(Network.TestNet).ToString(); //extended private key, can derive all addresses\keys from this
			ExtPubKey pubKey = pvtKey.Neuter(); //extended public key, can derive all public addresses from this
			string extPubKey = (pubKey).GetWif(Network.TestNet).ToString(); //extended public key string
			string pvtSrcKey = pvtKey.Derive(0).Derive(0).PrivateKey.ToString(Network.TestNet); //need this to spend btc
			string pubSrcAddr = pubKey.Derive(0).Derive(0).PubKey.GetAddress(Network.TestNet).ToString(); //source from bitcoin fountain (or USD)
			string pubRecAddr = pubKey.Derive(0).Derive(1).PubKey.GetAddress(Network.TestNet).ToString(); //use to receive funds
			string pubChgAddr = pubKey.Derive(1).Derive(0).PubKey.GetAddress(Network.TestNet).ToString(); //use as change address
			UpdateStatus("extPrvKey : " + extPrvKey);
			UpdateStatus("extPubKey : " + extPubKey);
			UpdateStatus("pvtSrcKey : " + pvtSrcKey);
			UpdateStatus("pubSrcAddr : " + pubSrcAddr);
			UpdateStatus("pubRecAddr : " + pubRecAddr);
			UpdateStatus("pubChgAddr : " + pubChgAddr);
		}

		async private void btnCreateTx_Click(object sender, RoutedEventArgs e) //create unsigned tx json
		{
			try
			{
				btnCreateTx.IsEnabled = false;
				UpdateStatus("Creating Transaction...");
				string extPubKey = txtExtPubKey.Text;
				string recAddr = txtRecAddr.Text;
				string chgAddr = txtChgAddr.Text;
				long sendAmt = long.Parse(txtSendAmt.Text);
				long feeAmt = long.Parse(txtFeeAmt.Text);
				string jsn = await Task.Run(()=> TxUtil.CreateTxJSON(extPubKey, recAddr, chgAddr, 0, sendAmt, feeAmt, IsTestNet));
				UpdateStatus("Transaction:\n" + jsn);
				txtTxJSON.Text = jsn;
			}
			catch (Exception ex)
			{
				UpdateStatus("Error: " + ex.Message);
			}
			btnCreateTx.IsEnabled = true;
		}

		private void btnAllKeys_Click(object sender, RoutedEventArgs e)
		{
			UpdateStatus(TxUtil.GetDerivedKeysAll(txtExtPvtKey.Text, 5, IsTestNet)); //derive addresses from ext private key
		}

		async private void btnEstFee_Click(object sender, RoutedEventArgs e)
		{
			try
			{
				txtFeeAmt.Text = "" +  await Task.Run(()=> TxUtil.EstimateFee());
				UpdateStatus("Estimated Fee = " + txtFeeAmt.Text);
			}
			catch (Exception ex)
			{
				UpdateStatus("Error: " + ex.Message);
			}
		}

		async private void btnSignTx_Click(object sender, RoutedEventArgs e) //create & sign tx from json
		{
			try
			{
				string txt = txtTxJSON.Text;
				string pvtKey = txtExtPvtKey.Text;
				string hex = await Task.Run(()=> TxUtil.SignTx(txt, pvtKey, IsTestNet));
				UpdateStatus("Tx Signed Hex = " + hex);
				txtTxSignedHex.Text = hex;
			}
			catch (Exception ex)
			{
				UpdateStatus("Error: " + ex.Message);
			}
		}

		async private void btnBroadcastTx_Click(object sender, RoutedEventArgs e) //broadcast tx to blockchain
		{
			try
			{
				string txt = txtTxSignedHex.Text;
				string hash = await Task.Run(()=> TxUtil.BroadcastTx(txt, IsTestNet));
				UpdateStatus("Tx Hash = " + hash);
				txtTxHash.Text = hash;
			}
			catch (Exception ex)
			{
				UpdateStatus("Error: " + ex.Message);
			}
		}

		async private void btnBalance_Click(object sender, RoutedEventArgs e) //get address balances from ElectrumX
		{
			try
			{
				string txt = txtExtPubKey.Text;
				long bal = await Task.Run(()=> TxUtil.GetExtendedBalance(txt, 0, 25, 10, IsTestNet));
				UpdateStatus("Ext Balance = " + bal + " satoshi (" + bal / 100000000d + " btc)");
			}
			catch (Exception ex)
			{
				UpdateStatus("Error: " + ex.Message);
			}
		}

		public void UpdateStatus(string msg) //add to status box in gui
		{
			Dispatcher.BeginInvoke(new Action<string>((m) =>
			{
				txtStatus.Text += DateTime.Now.ToString("yyyyMMdd:HHmmss ") + m + "\r\n";
				txtStatus.SelectionStart = txtStatus.Text.Length;
				txtStatus.ScrollToEnd();
			}), msg);
		}
	}
}
