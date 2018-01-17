﻿using System;
using System.Collections.Generic;
using System.Text;

namespace Stratis.SmartContracts.State
{
    /// <summary>
    /// Really magic class. Any time you commit(), you can get the root and use that to load the state at that particular time. 
    /// </summary>
    public class ContractStateRepositoryRoot : ContractStateRepository
    {
        private class StorageCache : ReadWriteCache<byte[]>
        {
            public ITrie<byte[]> trie;

            public StorageCache(ITrie<byte[]> trie) : base(new SourceCodec<byte[],byte[],byte[],byte[]>(trie, new Serializers.NoSerializer<byte[]>(), new Serializers.NoSerializer<byte[]>()), WriteCache<byte[]>.CacheType.SIMPLE)
            {
                this.trie = trie;
            }
        }

        private class MultiStorageCache : MultiCache<ICachedSource<byte[], byte[]>>
        {
            ContractStateRepositoryRoot parentRepo;

            public MultiStorageCache(ContractStateRepositoryRoot parentRepo) : base(null)
            {
                this.parentRepo = parentRepo;
            }

            protected override ICachedSource<byte[],byte[]> Create(byte[] key, ICachedSource<byte[], byte[]> srcCache)
            {
                AccountState accountState = this.parentRepo.accountStateCache.Get(key);
                PatriciaTrie storageTrie = this.parentRepo.CreateTrie(this.parentRepo.trieCache, accountState?.StateRoot);
                return new StorageCache(storageTrie);
            }

            protected override bool FlushChild(byte[] key, ICachedSource<byte[],byte[]> childCache)
            {
                if (base.FlushChild(key, childCache))
                {
                    if (childCache != null)
                    {
                        StorageCache storageChildCache = (StorageCache)childCache; // praying to the lord this works
                        AccountState storageOwnerAcct = this.parentRepo.accountStateCache.Get(key);
                        // need to update account storage root
                        storageChildCache.trie.Flush();
                        byte[] rootHash = storageChildCache.trie.GetRootHash();
                        storageOwnerAcct.StateRoot = rootHash;
                        this.parentRepo.accountStateCache.Put(key, storageOwnerAcct);
                        return true;
                    }
                    else
                    {
                        // account was deleted
                        return true;
                    }
                }
                else
                {
                    // no storage changes
                    return false;
                }
            }
        }

        private ISource<byte[], byte[]> stateDS;
        private ICachedSource<byte[], byte[]> trieCache;
        private ITrie<byte[]> stateTrie;

        public ContractStateRepositoryRoot(ISource<byte[],byte[]> stateDS) : this(stateDS, null) {}

        public ContractStateRepositoryRoot(ISource<byte[],byte[]> stateDS, byte[] root)
        {
            this.stateDS = stateDS;
            this.trieCache = new WriteCache<byte[]>(stateDS, WriteCache<byte[]>.CacheType.COUNTING);
            this.stateTrie = new PatriciaTrie(this.trieCache, root);
            SourceCodec<byte[], AccountState, byte[], byte[]> accountStateCodec = new SourceCodec<byte[], AccountState, byte[], byte[]>(this.stateTrie, new Serializers.NoSerializer<byte[]>(), Serializers.AccountSerializer);
            ReadWriteCache<AccountState> accountStateCache = new ReadWriteCache<AccountState>(accountStateCodec, WriteCache<AccountState>.CacheType.SIMPLE);

            MultiCache<ICachedSource<byte[], byte[]>> storageCache = new MultiStorageCache(this);
            ISource<byte[], byte[]> codeCache = new WriteCache<byte[]>(stateDS, WriteCache<byte[]>.CacheType.COUNTING);

            Init(accountStateCache, codeCache, storageCache);
        }

        public override void Commit()
        {
            base.Commit();
            this.stateTrie.Flush();
            this.trieCache.Flush();
        }

        public override byte[] GetRoot()
        {
            this.storageCache.Flush();
            this.accountStateCache.Flush();

            return this.stateTrie.GetRootHash();
        }

        public override void Flush()
        {
            Commit();
        }

        public override IContractStateRepository GetSnapshotTo(byte[] root)
        {
            return new ContractStateRepositoryRoot(this.stateDS, root);
        }

        public override void SyncToRoot(byte[] root)
        {
            this.stateTrie.SetRoot(root);
        }

        protected PatriciaTrie CreateTrie(ICachedSource<byte[], byte[]> trieCache, byte[] root)
        {
            return new PatriciaTrie(trieCache, root);
        }
    }
}
