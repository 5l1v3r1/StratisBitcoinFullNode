﻿using System;
using System.Collections.Generic;
using System.Linq;
using NBitcoin;

namespace Stratis.SmartContracts.State.AccountAbstractionLayer
{
    public class CondensingTx
    {
        /// <summary>
        /// Same as in Qtum. Can adjust later
        /// </summary>
        private const uint MAX_CONTRACT_VOUTS = 1000;

        private Dictionary<uint160, Tuple<ulong, ulong>> plusMinusInfo;
        private Dictionary<uint160, ulong> balances;
        private Dictionary<uint160, uint> nVouts;
        private Dictionary<uint160, Vin> vins;
        private IList<TransferInfo> transfers;
        private HashSet<uint160> deleteAddresses;
        private IContractStateRepository state;
        private SmartContractTransaction scTransaction;
        bool voutOverflow = false;

        public CondensingTx(IContractStateRepository state, IList<TransferInfo> transfers, SmartContractTransaction scTx, HashSet<uint160> deleteAddresses)
        {
            this.state = state;
            this.transfers = transfers;
            this.scTransaction = scTx;
            this.deleteAddresses = deleteAddresses;
        }

        public Transaction CreateCondensingTx()
        {
            SelectionVin();
            CalculatePlusAndMinus();
            if (!CreateNewBalances())
                return new Transaction();
            Transaction tx = new Transaction();
            foreach(var txIn in CreateVins())
            {
                tx.AddInput(txIn);
            }
            foreach(var txOut in CreateVout())
            {
                tx.AddOutput(txOut);
            }

            return !tx.Inputs.Any() || !tx.Outputs.Any() ? new Transaction() : tx;
        }

        private Dictionary<uint160, Vin> CreateVin(Transaction tx)
        {
            Dictionary<uint160, Vin> vins = new Dictionary<uint160, Vin>();

            foreach(var b in balances)
            {
                if (b.Key == scTransaction.From)
                    continue;

                if (b.Value > 0)
                {
                    vins[b.Key] = new Vin
                    {
                        Hash = tx.GetHash(),
                        Nvout = nVouts[b.Key],
                        Value = b.Value,
                        Alive = 1
                    };
                }
                else
                {
                    vins[b.Key] = new Vin
                    {
                        Hash = tx.GetHash(),
                        Nvout = 0,
                        Value = 0,
                        Alive = 0,
                    };
                }
            }
            return vins;
        }

        private void SelectionVin()
        {
            foreach(TransferInfo ti in transfers)
            {
                if (!vins.ContainsKey(ti.From))
                {
                    var a = state.Vin(ti.From);
                    if (a != null)
                    {
                        vins[ti.From] = a;
                    }
                    if (ti.From == scTransaction.From && scTransaction.Value > 0)
                    {
                        vins[ti.From] = new Vin
                        {
                            Hash = scTransaction.Hash,
                            Nvout = scTransaction.Nvout,
                            Value = scTransaction.Value,
                            Alive = 1
                        };
                    }
                }

                if (!vins.ContainsKey(ti.To))
                {
                    var a = state.Vin(ti.To);
                    if (a != null)
                        vins[ti.To] = a;
                }
            }
        }

        private void CalculatePlusAndMinus()
        {
            foreach(TransferInfo ti in transfers)
            {
                if (!plusMinusInfo.ContainsKey(ti.From))
                {
                    plusMinusInfo[ti.From] = new Tuple<ulong, ulong>(0, ti.Value);
                }
                else
                {
                    plusMinusInfo[ti.From] = new Tuple<ulong, ulong>(plusMinusInfo[ti.From].Item1, plusMinusInfo[ti.From].Item2 + ti.Value);
                }

                if (!plusMinusInfo.ContainsKey(ti.To))
                {

                    plusMinusInfo[ti.To] = new Tuple<ulong, ulong>(ti.Value, 0);
                }
                else
                {
                    plusMinusInfo[ti.To] = new Tuple<ulong, ulong>(plusMinusInfo[ti.To].Item1 + ti.Value, plusMinusInfo[ti.To].Item2);
                }
            }
        }

        private bool CreateNewBalances()
        {
            foreach(KeyValuePair<uint160, Tuple<ulong, ulong>> p in this.plusMinusInfo)
            {
                ulong balance = 0;
                if ((vins.ContainsKey(p.Key) && vins[p.Key].Alive != 0) 
                    || (vins[p.Key].Alive == 0 && !CheckDeleteAddress(p.Key)))
                {
                    balance = vins[p.Key].Value;
                }
                balance += p.Value.Item1;
                if (balance < p.Value.Item2)
                    return false;
                balance -= p.Value.Item2;
                balances[p.Key] = balance;
            }
            return true;
        }

        private IList<TxIn> CreateVins()
        {
            List<TxIn> txIns = new List<TxIn>();

            foreach(KeyValuePair<uint160, Vin> v in this.vins)
            {
                if(
                    (v.Value.Value > 0 && v.Value.Alive != 0)
                    || (v.Value.Value > 0 && this.vins[v.Key].Alive == 0 && !CheckDeleteAddress(v.Key)))
                {
                    OutPoint outpoint = new OutPoint(v.Value.Hash, v.Value.Nvout);
                    txIns.Add(new TxIn(outpoint, new Script(OpcodeType.OP_SPEND)));
                }
            }
            return txIns;
        }

        private IList<TxOut> CreateVout()
        {
            uint count = 0;
            List<TxOut> outs = new List<TxOut>();
            foreach(KeyValuePair<uint160, ulong> b in this.balances)
            {
                if (b.Value > 0)
                {
                    Script script =  null;
                    var a = state.GetAccountState(b.Key);
                    if (a != null)
                    {
                        // Create a send to contract
                        // script = CScript() << valtype{ 0} << valtype{ 0} << valtype{ 0} << valtype{ 0} << b.first.asBytes() << OP_CALL;
                        throw new NotImplementedException();
                    }
                    else
                    {
                        // Create a send to given address
                        // script = CScript() << OP_DUP << OP_HASH160 << b.first.asBytes() << OP_EQUALVERIFY << OP_CHECKSIG;
                        throw new NotImplementedException();
                    }
                    outs.Add(new TxOut(new Money(b.Value), script));
                    this.nVouts[b.Key] = count;
                    count++;
                    if (count > MAX_CONTRACT_VOUTS)
                    {
                        voutOverflow = true;
                        return outs;
                    }
                }
            }
            return outs;
        }

        private bool CheckDeleteAddress(uint160 address)
        {
            return this.deleteAddresses.Any(x => x == address);
        }
    }
}
