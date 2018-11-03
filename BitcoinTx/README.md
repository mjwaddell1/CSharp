This app demonstrates how to use the NBitcoin library to create a bitcoin transaction and sign it offline. The app is written in WPF usong VS 2017 and uses the NBitcoin Nuget package.
Using the application:
1)	Start the application
2)	There is a test extended key already hardcoded in the app. If you have your own extended private key, you can enter it into the Ext Private Key field and click the arrow button to get the extended public key and derived addresses used in transactions. If using your own key, you will need to fill in the first three fields in the form. The receive and change address are from the same extended address so the transaction will send bitcoin to the same extended address.
3)	Click the arrow button at the top of the form. This will retrieve the extended balance (first 10 addresses) for the extended public key.  If the total balance is low (below 0.001 BTC), you can get more test bitcoins from a bitcoin faucet (i.e. https://testnet-faucet.mempool.co/). Note that test coins are worthless except for testing.
4)	Enter an amount to send in the test transaction. 
5)	Click the arrow button next to the fee amount. This will retrieve the recommended transaction fee from the bitcoin blockchain. The sum of the send and fee amounts should be less than the total balance received in step 3.
6)	Click the Create Tx button. This will retrieve information from the blockchain needed for the transaction and serialize it to to json.
7)	Click the Sign Tx button this will take the json data and extended private key then use NHibernate to create a signed transaction hex. This step can be done offline (to protect the private key):
	- Copy the json data
	- Copy the extended private key
	- Start the app on an offline computer
	- Fill in the json and extended private key in the offline app
	- Click the Sign Tx button
	- Copy the signed transaction hex to the online application for broadcast
8)	Click the Broadcast Tx button. If broadcast is successful, a transaction hex should be returned. You can use this hex to look up you transaction on the test blockchain: https://live.blockcypher.com/btc-testnet/
Remember that your transaction is on the testnet blockchain. It will not appear on the mainnet (real bitcoin) chain.
Notes concerning the application:
	- The app is hardcoded to use testnet. You can switch to mainnet (real bitcoin) by using the flag at the top of MainWindow.xaml.cs.
	- The app may be used by several people so you will probably want to use your own extended private key. You can create a new random key using the code in MainWindow.xaml.cs (CreateAddresses)
	- The bitcoin node address and ElectrumX server address are hardcoded in the application (TxUtil.cs). If a node is down, you can find other servers here:
		- Mainnet ElectrumX & Bitcoin: https://uasf.saltylemon.org/electrum
		- Mainnet ElectrumX: https://1209k.com/bitcoin-eye/ele.php
		- Testnet ElectrumX: https://1209k.com/bitcoin-eye/ele.php?chain=tbtc


Questions and comments are welcome - mjwaddell {AT} hotmail.com
