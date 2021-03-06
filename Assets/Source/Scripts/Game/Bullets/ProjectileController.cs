using System;
using System.Collections.Generic;
using DG.Tweening;
using GGJ2022.Source.Scripts.Controls;
using GGJ2022.Source.Scripts.Game.Configs;
using GGJ2022.Source.Scripts.Game.Players;
using GGJ2022.Source.Scripts.Game.Players.Base;
using Photon.Pun;
using Photon.Pun.UtilityScripts;
using Sirenix.OdinInspector;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Zenject;

namespace GGJ2022.Source.Scripts.Game.Bullets
{
    public class ProjectileController : SerializedMonoBehaviour
    {
        public Dictionary<ObjectState, StateView> StateViews; 
        
        public PhotonView PhotonView;

        public Collider2D Collider;
        public Rigidbody2D Rigidbody2D;

        public Light2D SpotLight;
        
        private Vector3 _direciton;
        private Vector3 _startPosition;
        private BulletConfig _bulletConfig;
        private PlayerConfig _playerConfig;

        private ObjectState BulletState;
        private PhotonTeamsManager _teamsManager;
        private GameConfig _gameConfig;

        private bool _isDead;
        private bool _enableCollision;
        private StateView _currentState;

        [Inject]
        public void Construct(BulletConfig bulletConfig,
                              JoystickControlInfo controlInfo,
                              PlayerConfig playerConfig,
                              PhotonTeamsManager teamsManager,
                              GameConfig gameConfig)
        {
            _direciton = controlInfo.ShootDirection;
            _startPosition = transform.position;
            _bulletConfig = bulletConfig;
            _playerConfig = playerConfig;
            _teamsManager = teamsManager;
            _gameConfig = gameConfig;
        }

        private void LocalInitialize()
        {
            Destroy(Rigidbody2D);
            Destroy(Collider);

            _currentState ??= _currentState = StateViews[BulletState];
            _currentState.Attack.gameObject.SetActive(true);
        }

        public void Initialize(ObjectState initialState)
        {
            BulletState = initialState;
           
            _currentState ??= _currentState = StateViews[BulletState];
            _currentState.Attack.gameObject.SetActive(true);
            PhotonView.RPC("UpdateBulletType", RpcTarget.Others, initialState);
            PhotonView.RPC("UpdateBulletRotation", RpcTarget.All, _direciton);
            PhotonView.RPC("ChangeBulletLightColor", RpcTarget.All);
            _enableCollision = true;
        }

        [PunRPC]
        private void UpdateBulletRotation(Vector3 direction)
        {
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            _currentState.Attack.transform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);
        }

        [PunRPC]
        private void ShowDeathAnimation()
        {
            _currentState.Attack.gameObject.SetActive(false);
            _currentState.Hit.gameObject.SetActive(true);
            DOTween.To(() => SpotLight.intensity, value => SpotLight.intensity = value, 0, _bulletConfig.LightFadeOutTime);
        }

        [PunRPC]
        private void UpdateBulletType(ObjectState state)
        {
            BulletState = state;

            LocalInitialize();
        }

        [PunRPC]
        private void ChangeBulletLightColor()
        {
            SpotLight.color = _bulletConfig.GetRightColor(BulletState);
        }

        private void OnCollisionEnter2D(Collision2D other)
        {
            if (_enableCollision)
            {
                _isDead = true;
                PhotonView.RPC("ShowDeathAnimation", RpcTarget.All);
             
                _currentState ??= _currentState = StateViews[BulletState];
                var isPlayerCollision =
                    other.gameObject.layer == LayerMask.NameToLayer(PlayerLayerController.OtherLayer);

                // If other player
                if (PhotonView.IsMine && isPlayerCollision)
                {
                    var gameObjectOther = other.gameObject.GetComponent<PhotonView>();

                    var playerTypeController = gameObjectOther.GetComponent<PlayerTypeController>();

                    if (playerTypeController.Type == ObjectState.First)
                    {
                        gameObjectOther.RPC("PlayAngelHitSound", RpcTarget.All);
                    }
                    else
                    {
                        gameObjectOther.RPC("PlayDevilHitSound", RpcTarget.All);
                    }

                    _teamsManager.TryGetTeamMatesOfPlayer(PhotonView.Controller, out var teamMates);

                    var otherPlayer = gameObjectOther.Controller;

                    var playerManager = gameObjectOther.GetComponent<PlayerManager>();
                    var isBulletState = playerTypeController.Type == BulletState;
                    var isNotBulletState = playerTypeController.Type != BulletState;
                    // This is your ally
                    if (teamMates != null && teamMates.Length > 0 && teamMates[0].UserId == otherPlayer.UserId)
                    {
                        // Heal
                        if (isBulletState)
                            playerManager.Heal(_playerConfig.AllyHeal);
                        else if (isNotBulletState && _gameConfig.FriendlyFire)
                            playerManager.Damage(_playerConfig.FriendlyFireDamage);
                    }
                    else
                    {
                        if (isBulletState && _gameConfig.IsEnemyHeal)
                            playerManager.Heal(_playerConfig.EnemyHeal);
                        else if (isNotBulletState)
                            playerManager.Damage(_playerConfig.Damage);
                    }
                }

                if (!isPlayerCollision)
                {
                    PhotonView.RPC("PlayWallHitSound", RpcTarget.All);
                }

                if (PhotonView.IsMine)
                {
                    _currentState.Hit.AnimationState.Complete += entry => { PhotonNetwork.Destroy(gameObject); };
                }
            }
        }

        private void Update()
        {
            _currentState ??= _currentState = StateViews[BulletState];
            
            var isDistanceExceed = Vector2.Distance(transform.position, _startPosition) > _playerConfig.FireDistance;
            if (PhotonView.IsMine &&  !isDistanceExceed && !_isDead)
            {
                transform.position += _direciton * _bulletConfig.BulletSpeed * Time.deltaTime;
            }

            if (PhotonView.IsMine && isDistanceExceed & !_isDead)
            {
                _isDead = true;
                
                PhotonView.RPC("ShowDeathAnimation", RpcTarget.All);
                _currentState.Hit.AnimationState.Complete += _ =>
                {
                    PhotonNetwork.Destroy(gameObject);
                };
            }
        }
    }
}