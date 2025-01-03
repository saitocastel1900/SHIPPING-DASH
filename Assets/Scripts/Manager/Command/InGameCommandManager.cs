﻿using System;
using UnityEngine;
using System.Collections.Generic;
using Commons.Const;
using Zenject;
using UniRx;
using Block;
using Commons.Save;
using Input;
using Manager.Stage;
using Player;

namespace Manager.Command
{
    /// <summary>
    ///　移動コマンドを管理する
    /// </summary>
    public class InGameCommandManager : MonoBehaviour
    {
        /// <summary>
        /// 配達したら呼ばれる
        /// </summary>
        public IReactiveProperty<bool> IsDelivered => _isDelivered;
        private readonly BoolReactiveProperty _isDelivered = new BoolReactiveProperty(false);

        /// <summary>
        /// StageManager
        /// </summary>
        [SerializeField] private StageGenerator _stageManager;

        /// <summary>
        /// AudioManager
        /// </summary>
        [Inject] private AudioManager _audioManager;
        
        /// <summary>
        /// 入力
        /// </summary>
        [Inject] private IInputEventProvider _input;

        /// <summary>
        /// SaveManager
        /// </summary>
        [Inject] private SaveManager _saveManager;

        /// <summary>
        /// 進行方向のリスト
        /// </summary>
        private readonly Stack<Vector3> _directionList = new Stack<Vector3>();
        
        /// <summary>
        /// コマンドのリスト
        /// </summary>
        public IReactiveCollection<bool> BlockMovedList => blockMovedList;
        private ReactiveCollection<bool> blockMovedList = new ReactiveCollection<bool>();
        
        /// <summary>
        /// ブロックが移動したか
        /// </summary>
        private readonly Stack<bool> _blockIsMovedList = new Stack<bool>();

        /// <summary>
        /// Playerのキャッシュ
        /// </summary>
        private GameObject _player;

        /// <summary>
        /// 初期化
        /// </summary>
        public void Initialize()
        {
           _input
              .MoveDirection
              .SkipLatestValueOnSubscribe()
              .Where(_=>_input.IsPlayGame.Value)
              .Where(_=>_saveManager.Data.CurrentStageNumber<=_saveManager.Data.MaxStageClearNumber)
              .ThrottleFirst(TimeSpan.FromSeconds(0.25f))
                .Subscribe(OnMove)
                .AddTo(this.gameObject);
           
            _input
                .IsUndo
                .SkipLatestValueOnSubscribe()
                .Subscribe(_ =>
                {
                    OnUndo();
                    _audioManager.PlaySoundEffect(SoundEffectData.SoundEffect.PushedUndoButton);
                });
        }

        /// <summary>
        /// リセット
        /// </summary>
        public void Reset()
        {
            _isDelivered.Value = false;
            _directionList.Clear();
            _blockIsMovedList.Clear();
            blockMovedList.Clear();
        }

        /// <summary>
        /// 移動
        /// </summary>
        /// <param name="direction"></param>
        public void OnMove(Vector3 direction)
        {
            _player = GameObject.FindWithTag(Const.PlayerObjectTag);
            
            bool blockIsMoved = false;
            
            var currentPlayerPos = _stageManager.GetObjectPosition(_player);
            var nextPlayerPos = _stageManager.GetNextPosition(currentPlayerPos, direction);

            //進行方向がステージ内なら移動する
            if (!_stageManager.IsValidPosition(nextPlayerPos)) return;
            _directionList.Push(direction);

            //進行方向にブロックが存在していたら、、、
            if (_stageManager.IsBlockPosition(nextPlayerPos))
            {
                // ブロックの移動先の位置を計算
                var nextBlockPos = _stageManager.GetNextPosition(nextPlayerPos, direction);

                // ブロックの移動先がステージ内の場合かつブロックの移動先にブロックが存在しない場合
                if (_stageManager.IsValidPosition(nextBlockPos) && !_stageManager.IsBlockPosition(nextBlockPos))
                {
                    // 移動するブロックを取得
                    var block = _stageManager.GetAtPositionObject(new Vector3Int(nextPlayerPos.x, 0,
                        nextPlayerPos.z));

                    // プレイヤーの移動先のタイルの情報を更新
                    _stageManager.UpdateObjectPosition(nextPlayerPos);

                    // ブロックを移動
                    ICommand blockCommand = new MoveCommand(block.GetComponent<BlockMover>(), direction);
                    BlockMoveCommandInvoker.Execute(blockCommand);

                    // ブロックの位置を更新
                    _stageManager.SetObjectPosition(block, new Vector3Int(nextBlockPos.x, 0, nextBlockPos.z));

                    // ブロックの移動先の番号を更新
                    switch (_stageManager.GetTileType(nextBlockPos.x, nextBlockPos.z))
                    {
                        case TileType.GROUND:
                            _stageManager.SetTileType(nextBlockPos.x, nextBlockPos.z, TileType.BLOCK);
                            break;

                        case TileType.TARGET:
                            _stageManager.SetTileType(nextBlockPos.x, nextBlockPos.z, TileType.BLOCK_ON_TARGET);
                            break;
                    }

                    // プレイヤーの現在地のタイルの情報を更新
                    _stageManager.UpdateObjectPosition(currentPlayerPos);

                    // プレイヤーを移動
                    ICommand playerCommand = new MoveCommand(_player.GetComponent<PlayerMover>(), direction);
                    PlayerMoveCommandInvoker.Execute(playerCommand);

                    // プレイヤーの位置を更新
                    _stageManager.SetObjectPosition(_player, nextPlayerPos);

                    //タイルを上書き
                    switch (_stageManager.GetTileType(nextPlayerPos.x, nextPlayerPos.z))
                    {
                        case TileType.GROUND:
                            _stageManager.SetTileType(nextPlayerPos.x, nextPlayerPos.z, TileType.PLAYER);
                            break;

                        case TileType.TARGET:
                            _stageManager.SetTileType(nextPlayerPos.x, nextPlayerPos.z, TileType.PLAYER_ON_TARGET);
                            break;
                    }

                    blockIsMoved = true;
                }
            }
            else
            {
                // プレイヤーの現在地のタイルの情報を更新
                _stageManager.UpdateObjectPosition(currentPlayerPos);

                // プレイヤーを移動
                ICommand playerCommand = new MoveCommand(_player.GetComponent<PlayerMover>(), direction);
                PlayerMoveCommandInvoker.Execute(playerCommand);

                // プレイヤーの位置を更新
                _stageManager.SetObjectPosition(_player, nextPlayerPos);

                //タイルを上書き
                switch (_stageManager.GetTileType(nextPlayerPos.x, nextPlayerPos.z))
                {
                    case TileType.GROUND:
                        _stageManager.SetTileType(nextPlayerPos.x, nextPlayerPos.z, TileType.BLOCK);
                        break;

                    case TileType.TARGET:
                        _stageManager.SetTileType(nextPlayerPos.x, nextPlayerPos.z, TileType.BLOCK_ON_TARGET);
                        break;
                }
            }

            _isDelivered.Value = _stageManager.IsDelivered();
            _blockIsMovedList.Push(blockIsMoved);
            blockMovedList.Add(true);
        }

        /// <summary>
        /// 移動巻き戻し
        /// </summary>
        private void OnUndo()
        {
            //移動していたら移動を巻き戻す
            if (_directionList.Count > 0)
            {
                var direction = _directionList.Pop();
                
                var currentPlayerPos = _stageManager.GetObjectPosition(_player);
                var nextPlayerPos = _stageManager.GetNextPosition(currentPlayerPos, -direction);
                
                //ブロックが動かされていたら、ブロックも移動を巻き戻す
                if (_blockIsMovedList.Count > 0 && _blockIsMovedList.Pop())
                {
                    var currentBlockPos = _stageManager.GetNextPosition(currentPlayerPos, direction);

                    // ブロックの移動先のタイルの情報を更新
                    _stageManager.UpdateObjectPosition(currentBlockPos);

                    // 移動するブロックを取得
                    var block = _stageManager.GetAtPositionObject(new Vector3Int(currentBlockPos.x, 0,
                        currentBlockPos.z));

                    //　ブロックを移動
                    BlockMoveCommandInvoker.Undo();

                    // ブロックの位置を更新
                    _stageManager.SetObjectPosition(block, new Vector3Int(currentPlayerPos.x, 0, currentPlayerPos.z));

                    //タイルを上書き
                    switch (_stageManager.GetTileType(currentPlayerPos.x, currentPlayerPos.z))
                    {
                        case TileType.PLAYER:
                            _stageManager.SetTileType(currentPlayerPos.x, currentPlayerPos.z, TileType.BLOCK);
                            break;

                        case TileType.TARGET:
                            _stageManager.SetTileType(currentPlayerPos.x, currentPlayerPos.z, TileType.BLOCK_ON_TARGET);
                            break;
                    }
                    
                    // プレイヤーの現在地のタイルの情報を更新
                    _stageManager.UpdateObjectPosition(nextPlayerPos);
                    
                    // プレイヤーを移動
                    PlayerMoveCommandInvoker.Undo();
                    
                    // プレイヤーの位置を更新
                    _stageManager.SetObjectPosition(_player, nextPlayerPos);

                    //タイルを上書き
                    switch (_stageManager.GetTileType(nextPlayerPos.x, nextPlayerPos.z))
                    {
                        case TileType.GROUND:
                            _stageManager.SetTileType(nextPlayerPos.x, nextPlayerPos.z, TileType.PLAYER);
                            break;

                        case TileType.TARGET:
                            _stageManager.SetTileType(nextPlayerPos.x, nextPlayerPos.z, TileType.PLAYER_ON_TARGET);
                            break;
                    }
                    
                    blockMovedList.RemoveAt(_blockIsMovedList.Count-1);
                }
                else
                {
                    // プレイヤーの現在地のタイルの情報を更新
                    _stageManager.UpdateObjectPosition(currentPlayerPos);
                    
                    // プレイヤーを移動
                    PlayerMoveCommandInvoker.Undo();
                    
                    // プレイヤーの位置を更新
                    _stageManager.SetObjectPosition(_player, nextPlayerPos);

                    //タイルを上書き
                    switch (_stageManager.GetTileType(nextPlayerPos.x, nextPlayerPos.z))
                    {
                        case TileType.GROUND:
                            _stageManager.SetTileType(nextPlayerPos.x, nextPlayerPos.z, TileType.PLAYER);
                            break;

                        case TileType.TARGET:
                            _stageManager.SetTileType(nextPlayerPos.x, nextPlayerPos.z, TileType.PLAYER_ON_TARGET);
                            break;
                    }
                    
                    blockMovedList.RemoveAt(_blockIsMovedList.Count-1);
                }
            }
        }
    }
}
