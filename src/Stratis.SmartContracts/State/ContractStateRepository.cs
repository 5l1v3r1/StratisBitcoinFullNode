﻿using DBreeze;
using DBreeze.Utils;
using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using Stratis.SmartContracts.Hashing;
using NBitcoin;

namespace Stratis.SmartContracts.State
{
    public class ContractStateRepository : IContractStateRepository
    {
        protected ContractStateRepository parent;
        public ISource<byte[], AccountState> accountStateCache;
        protected ISource<byte[], byte[]> codeCache;
        protected MultiCache<ICachedSource<byte[], byte[]>> storageCache;

        protected ContractStateRepository() { }

        public ContractStateRepository(ISource<byte[], AccountState> accountStateCache, ISource<byte[], byte[]> codeCache,
                      MultiCache<ICachedSource<byte[], byte[]>> storageCache)
        {
            Init(accountStateCache, codeCache, storageCache);
        }

        protected void Init(ISource<byte[], AccountState> accountStateCache, ISource<byte[], byte[]> codeCache,
                    MultiCache<ICachedSource<byte[], byte[]>> storageCache)
        {
            this.accountStateCache = accountStateCache;
            this.codeCache = codeCache;
            this.storageCache = storageCache;
        }

        public AccountState CreateAccount(uint160 addr)
        {
            AccountState state = new AccountState();
            this.accountStateCache.Put(addr.ToBytes(), state);
            return state;
        }

        public bool IsExist(uint160 addr)
        {
            return GetAccountState(addr) != null;
        }

        public AccountState GetAccountState(uint160 addr)
        {
            return this.accountStateCache.Get(addr.ToBytes());
        }

        private AccountState GetOrCreateAccountState(uint160 addr)
        {
            AccountState ret = this.accountStateCache.Get(addr.ToBytes());
            if (ret == null)
            {
                ret = CreateAccount(addr);
            }
            return ret;
        }

        public void Delete(uint160 addr)
        {
            this.accountStateCache.Delete(addr.ToBytes());
            this.storageCache.Delete(addr.ToBytes());
        }

        public void SaveCode(uint160 addr, byte[] code)
        {
            byte[] codeHash = HashHelper.Keccak256(code);
            this.codeCache.Put(codeHash, code);
            AccountState accountState = GetOrCreateAccountState(addr);
            accountState.CodeHash = codeHash;
            this.accountStateCache.Put(addr.ToBytes(), accountState);
        }

        public byte[] GetCode(uint160 addr)
        {
            byte[] codeHash = GetCodeHash(addr);
            return this.codeCache.Get(codeHash);
        }

        public byte[] GetCodeHash(uint160 addr)
        {
            AccountState accountState = GetAccountState(addr);
            return accountState != null ? accountState.CodeHash : new byte[0]; // TODO: REPLACE THIS BYTE0 with something
        }

        public void AddStorageRow(uint160 addr, byte[] key, byte[] value)
        {
            GetOrCreateAccountState(addr);
            ISource<byte[], byte[]> contractStorage = this.storageCache.Get(addr.ToBytes());
            contractStorage.Put(key, value); // TODO: Check if 0
        }

        public byte[] GetStorageValue(uint160 addr, byte[] key)
        {
            AccountState accountState = GetAccountState(addr);
            return accountState == null ? null : this.storageCache.Get(addr.ToBytes()).Get(key);
        }

        public IContractStateRepository StartTracking()
        {
            ISource<byte[], AccountState> trackAccountStateCache = new WriteCache<AccountState>(this.accountStateCache,
                    WriteCache<AccountState>.CacheType.SIMPLE);
            ISource<byte[], byte[]> trackCodeCache = new WriteCache<byte[]>(this.codeCache, WriteCache< byte[]>.CacheType.SIMPLE);
            MultiCache<ICachedSource<byte[], byte[]>> trackStorageCache = new RealMultiCache(this.storageCache);
            ContractStateRepository ret = new ContractStateRepository(trackAccountStateCache, trackCodeCache, trackStorageCache);
            ret.parent = this;
            return ret;
        }

        public virtual IContractStateRepository GetSnapshotTo(byte[] root)
        {
            return this.parent.GetSnapshotTo(root);
        }

        public virtual void Commit()
        {
            ContractStateRepository parentSync = this.parent == null ? this : this.parent;
            lock(parentSync) {
                this.storageCache.Flush();
                this.codeCache.Flush();
                this.accountStateCache.Flush();
            }
        }

        public virtual void Rollback()
        {
            // nothing to do, will be GCed
        }

        public virtual byte[] GetRoot()
        {
            throw new Exception("Not supported");
        }

        public virtual void Flush()
        {
            throw new Exception("Not supported");
        }

        public virtual void SyncToRoot(byte[] root)
        {
            throw new Exception("Not supported");
        }
    }
}