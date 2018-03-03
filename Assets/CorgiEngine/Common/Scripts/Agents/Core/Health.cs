using UnityEngine;
using System.Collections;
using MoreMountains.Tools;

namespace MoreMountains.CorgiEngine
{
	/// <summary>
	/// This class manages the health of an object, pilots its potential health bar, handles what happens when it takes damage,
	/// and what happens when it dies.
	/// </summary>
	[AddComponentMenu("Corgi Engine/Character/Core/Health")] 
	public class Health : MonoBehaviour
	{
		/// the current health of the character
		[ReadOnly]
		public int CurrentHealth ;
		/// If this is true, this object can't take damage
		[ReadOnly]
		public bool Invulnerable = false;	

		[Header("Health")]
		[Information("Add this component to an object and it'll have health, will be able to get damaged and potentially die.",MoreMountains.Tools.InformationAttribute.InformationType.Info,false)]
		/// the initial amount of health of the object
	    public int InitialHealth = 10;
	    /// the maximum amount of health of the object
	    public int MaximumHealth = 10;

		[Header("Damage")]
		[Information("Here you can specify an effect and a sound FX to instantiate when the object gets damaged, and also how long the object should flicker when hit (only works for sprites).",MoreMountains.Tools.InformationAttribute.InformationType.Info,false)]
		/// the effect that will be instantiated everytime the character touches the ground
		public GameObject DamageEffect;
		// the sound to play when the player gets hit
		public AudioClip DamageSfx;
		// should the sprite (if there's one) flicker when getting damage ?
		public bool FlickerSpriteOnHit = true;

		[Header("Death")]
		[Information("Here you can set an effect to instantiate when the object dies, a force to apply to it (corgi controller required), how many points to add to the game score, and if the device should vibrate (only works on iOS and Android).",MoreMountains.Tools.InformationAttribute.InformationType.Info,false)]
		/// the effect to instantiate when the object gets destroyed
		public GameObject DeathEffect;
		/// if this is true, collisions will be turned off when the character dies
		public bool CollisionsOffOnDeath = true;
		/// the force applied when the character dies
		public Vector2 DeathForce = new Vector2(0,10);
		/// the points the player gets when the object's health reaches zero
		public int PointsWhenDestroyed;
		/// if true, the handheld device will vibrate when the object dies
		public bool VibrateOnDeath;

		// respawn
		public delegate void OnReviveDelegate();
		public OnReviveDelegate OnRevive;

		protected Color _initialColor;
		protected Color _flickerColor = new Color32(255, 20, 20, 255); 
		protected SpriteRenderer _spriteRenderer;
		protected Character _character;
		protected CorgiController _controller;
	    protected HealthBar _healthBar;
	    protected Collider2D _collider2D;

	    /// <summary>
	    /// On Start, we initialize our health
	    /// </summary>
	    protected virtual void Start()
	    {
			Initialization();
	    }

	    /// <summary>
	    /// Grabs useful components, enables damage and gets the inital color
	    /// </summary>
		protected virtual void Initialization()
		{
			_spriteRenderer = GetComponent<SpriteRenderer>();
			_character = GetComponent<Character>();
			_controller = GetComponent<CorgiController>();
			_healthBar = GetComponent<HealthBar>();
			_collider2D = GetComponent<Collider2D>();

			CurrentHealth = InitialHealth;
			DamageEnabled();

			if (_spriteRenderer != null)
			{
				if (_spriteRenderer.material.HasProperty("_Color"))
				{
					_initialColor = _spriteRenderer.material.color;
				}
			}
		}

		/// <summary>
		/// When the object is enabled (on respawn for example), we restore its initial health levels
		/// </summary>
	    protected virtual void OnEnable()
	    {
			CurrentHealth = InitialHealth;
			DamageEnabled();
			UpdateHealthBar ();
	    }

		/// <summary>
		/// Called when the object takes damage
		/// </summary>
		/// <param name="damage">The amount of health points that will get lost.</param>
		/// <param name="instigator">The object that caused the damage.</param>
		/// <param name="flickerDuration">The time (in seconds) the object should flicker after taking the damage.</param>
		/// <param name="invincibilityDuration">The duration of the short invincibility following the hit.</param>
		public virtual void Damage(int damage,GameObject instigator, float flickerDuration, float invincibilityDuration)
		{
			// if the object is invulnerable, we do nothing and exit
			if (Invulnerable)
			{
				return;
			}

			// if we're already below zero, we do nothing and exit
			if ((CurrentHealth <= 0) && (InitialHealth != 0))
			{
				return;
			}

			// we decrease the character's health by the damage
			float previousHealth = CurrentHealth;
			CurrentHealth -= damage;

			// we prevent the character from colliding with Projectiles, Player and Enemies
			if (invincibilityDuration > 0)
			{
				DamageDisabled();
				StartCoroutine(DamageEnabled(invincibilityDuration));	
			}

			// we trigger a damage taken event
			MMEventManager.TriggerEvent(new MMDamageTakenEvent(_character, instigator, CurrentHealth, damage, previousHealth));

			// we play the sound the player makes when it gets hit
			PlayHitSfx();
					
			// When the character takes damage, we create an auto destroy hurt particle system
	        if (DamageEffect!= null)
	        { 
	    		Instantiate(DamageEffect,transform.position,transform.rotation);
	        }

			if (FlickerSpriteOnHit)
			{
				// We make the character's sprite flicker
				if (_spriteRenderer != null)
				{
					StartCoroutine(MMImage.Flicker(_spriteRenderer,_initialColor,_flickerColor,0.05f,flickerDuration));	
				}	
			}

			// we update the health bar
			UpdateHealthBar();

			// if health has reached zero
			if (CurrentHealth <= 0)
			{
				// we set its health to zero (useful for the healthbar)
				CurrentHealth = 0;
				if (_character != null)
				{
					if (_character.CharacterType == Character.CharacterTypes.Player)
					{
						LevelManager.Instance.KillPlayer(_character);
						return;
					}
				}

				Kill();
			}
		}

		/// <summary>
		/// Kills the character, vibrates the device, instantiates death effects, handles points, etc
		/// </summary>
		public virtual void Kill()
		{
			// we make our handheld device vibrate
			if (VibrateOnDeath)
			{
				#if UNITY_ANDROID || UNITY_IPHONE
					Handheld.Vibrate();	
				#endif
			}

			// we prevent further damage
			DamageDisabled();

			// instantiates the destroy effect
			if (DeathEffect!=null)
			{
				var instantiatedEffect=(GameObject)Instantiate(DeathEffect,transform.position,transform.rotation);
	            instantiatedEffect.transform.localScale = transform.localScale;
			}

			// Adds points if needed.
			if(PointsWhenDestroyed != 0)
			{
				GameManager.Instance.AddPoints(PointsWhenDestroyed);
			}

			// if we have a controller, removes collisions, restores parameters for a potential respawn, and applies a death force
			if (_controller != null)
			{
				// we make it ignore the collisions from now on
				if (CollisionsOffOnDeath)
				{
					_controller.CollisionsOff();	
					if (_collider2D != null)
					{
						_collider2D.enabled = false;
					}
				}

				// we reset our parameters
				_controller.ResetParameters();

				// we apply our death force
				if (DeathForce != Vector2.zero)
				{
					_controller.SetForce(DeathForce);		
				}
			}

			// if we have a character, we want to change its state
			if (_character != null)
			{
				// we set its dead state to true
				_character.ConditionState.ChangeState(CharacterStates.CharacterConditions.Dead);
				_character.Reset ();

				// if this is a player, we quit here
				if (_character.CharacterType == Character.CharacterTypes.Player)
				{
					return;
				}
			}

			// finally we destroy the object
			DestroyObject();
		}

		/// <summary>
		/// Revive this object.
		/// </summary>
		public virtual void Revive()
		{
			if (_collider2D != null)
			{
				_collider2D.enabled = true;
			}
			if (_controller != null)
			{
				_controller.CollisionsOn();
				_controller.SetForce(Vector2.zero);
				_controller.ResetParameters();
			}
			if (_character != null)
			{
				_character.ConditionState.ChangeState(CharacterStates.CharacterConditions.Normal);
			}
			Initialization();
			UpdateHealthBar();

			if (OnRevive != null)
			{
				OnRevive ();
			}
		}

	    /// <summary>
	    /// Destroys the object
	    /// </summary>
	    protected virtual void DestroyObject()
		{
			// object is turned inactive to be able to reinstate it at respawn
			gameObject.SetActive(false);
		}

		/// <summary>
		/// Called when the character gets health (from a stimpack for example)
		/// </summary>
		/// <param name="health">The health the character gets.</param>
		/// <param name="instigator">The thing that gives the character health.</param>
		public virtual void GetHealth(int health,GameObject instigator)
		{
			// this function adds health to the character's Health and prevents it to go above MaxHealth.
			CurrentHealth = Mathf.Min (CurrentHealth + health,MaximumHealth);
			UpdateHealthBar();
		}

		/// <summary>
		/// Plays a sound when the character is hit
		/// </summary>
		protected virtual void PlayHitSfx()
	    {
			if (DamageSfx!=null)
			{
				SoundManager.Instance.PlaySound(DamageSfx,transform.position);
			}
	    }

	    /// <summary>
	    /// Resets the character's health to its max value
	    /// </summary>
	    public virtual void ResetHealthToMaxHealth()
	    {
			CurrentHealth = MaximumHealth;
			UpdateHealthBar ();
	    }	

	    /// <summary>
	    /// Updates the character's health bar progress.
	    /// </summary>
	    protected virtual void UpdateHealthBar()
	    {
	    	if (_healthBar != null)
	    	{
				_healthBar.UpdateBar(CurrentHealth,0f,MaximumHealth);
	    	}

	    	if (_character != null)
	    	{
	    		if (_character.CharacterType == Character.CharacterTypes.Player)
	    		{
					// We update the health bar
					if (GUIManager.Instance != null)
					{
						GUIManager.Instance.UpdateHealthBar(CurrentHealth,0f,MaximumHealth,_character.PlayerID);
					}
	    		}
	    	}
	    }

	    /// <summary>
	    /// Prevents the character from taking any damage
	    /// </summary>
	    public virtual void DamageDisabled()
	    {
			Invulnerable = true;
	    }

	    /// <summary>
	    /// Allows the character to take damage
	    /// </summary>
	    public virtual void DamageEnabled()
	    {
	    	Invulnerable = false;
	    }

		/// <summary>
	    /// makes the character able to take damage again after the specified delay
	    /// </summary>
	    /// <returns>The layer collision.</returns>
	    public virtual IEnumerator DamageEnabled(float delay)
		{
			yield return new WaitForSeconds (delay);
			Invulnerable = false;
		}
	}
}