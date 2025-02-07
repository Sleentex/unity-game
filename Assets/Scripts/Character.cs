﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum CharacterState
{
    Idle,
    Jump,
    Run,
    Dizzy,
}

public class Character : Unit
{
    const float INTERVAL_DAMAGE = 0.5F;

    [SerializeField]
    private float speed = 5.0F;

    [SerializeField]
    private float jumpForce = 15.0F;

    [SerializeField]
    private GameObject respawn; // add now maybe need to change

    [SerializeField] // щоб було видно в юніті
    private int lives = 5;
    public int Lives
    {
        get { return lives; }
        set 
        { 
            if (value <= 5) lives = value;
            livesBar.Refresh();
        }
    }

    private LivesBar livesBar;
    private Vector3 direction;
    private Bullet bullet;
    private bool isGrounded = false;
    private Animator animator;
    private SpriteRenderer sprite;


    private CharacterState State
    {
        get { return (CharacterState) animator.GetInteger("State"); }
        set { animator.SetInteger("State", (int) value); }
    }


    new private Rigidbody2D rigidbody;
    public Rigidbody2D Rigidbody
    {
        get { return rigidbody; }
        set { rigidbody = value; }
    }

    private float lastRecievedDamageTime = 0;
    private float LastRecievedDamageTime
    {
        get { return lastRecievedDamageTime; }
        set { lastRecievedDamageTime = value; }
    }

    [SerializeField]
    private AudioClip audioDie;
    [SerializeField]
    private AudioClip audioJump;
    [SerializeField]
    private AudioClip audioCongratulations;
    [SerializeField]
    private AudioClip audioDamage;

    private AudioSource audioSource;
    private AudioSource childAudioSource;

    private void Start()
    {
        transform.position = respawn.transform.position;
    }


    private void Awake()
    {
        
        audioSource = GetComponent<AudioSource>();
        childAudioSource = GetComponentsInChildren<AudioSource>()[1];
        livesBar = FindObjectOfType<LivesBar>();
        rigidbody = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
        sprite = GetComponentInChildren<SpriteRenderer>();

        bullet = Resources.Load<Bullet>("Bullet"); // "Bullet" - назва пребафа, який знаходиться зараз в папці Resources
    }


    // це краще робити для фізики, тому що він фіксірується в визначений час (як я поняв, то менше чим Update)
    private void FixedUpdate()
    {
        CheckGround();
    }



    private void Update()
    {
        if (Time.timeScale == 1F) // щоб не виконувало дії при меню паузи
        {
            if (Time.time > LastRecievedDamageTime + INTERVAL_DAMAGE && sprite.material.color == Color.red) sprite.material.color = Color.white;

            if (Input.GetKeyDown(KeyCode.M)) AudioListener.pause = !AudioListener.pause;
            if (Input.GetKeyDown(KeyCode.N)) audioSource.mute = !audioSource.mute; 
            if (Input.GetKeyDown(KeyCode.B)) childAudioSource.mute = !childAudioSource.mute;

            if (isGrounded) State = CharacterState.Idle;
            //if (Input.GetButtonDown("Fire1")) Shoot(); // Left Ctrl
            if (Input.GetButton("Horizontal")) Run();
            if (Time.time > LastRecievedDamageTime + INTERVAL_DAMAGE)
            {
                if (isGrounded && Input.GetButtonDown("Jump")) Jump();
            }
            
        }
    }



    private void Run()
    {
        // Input.GetAxis("Horizontal") = якщо вправо то вертає 1, якщо в ліво то -1
        direction = transform.right * Input.GetAxis("Horizontal"); 

        transform.position = Vector3.MoveTowards(transform.position, transform.position + direction, speed * Time.deltaTime);

        sprite.flipX = direction.x < 0.0F;

         if (isGrounded) State = CharacterState.Run;
    }



    private void Jump()
    {
        audioSource.clip = audioJump;
        audioSource.Play();
        State = CharacterState.Jump;

        rigidbody.AddForce(transform.up * jumpForce, ForceMode2D.Impulse);
    }



    private void Shoot()
    {
        Vector3 position = transform.position; 
        position.y += 0.8F; // поява кулі

        Bullet newBullet = Instantiate(bullet, position, bullet.transform.rotation) as Bullet; // создание пули в сцене

        newBullet.Parent = gameObject; // щоб знати хто створив пулю
        newBullet.Direction = newBullet.transform.right * (sprite.flipX ? -1.0F : 1.0F); // щоб знати з якого боку появилась пуля
    }

    private void OnDrawGizmos()
    {
        Vector3 tr = transform.position;
        tr.y += 0.2F;

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(tr, 0.3F);

    }


    private void CheckGround()
    {

        if (Time.time <= LastRecievedDamageTime + INTERVAL_DAMAGE)
        {
            State = CharacterState.Dizzy;
        }
        else
        {

            Vector2 tr = transform.position;
            tr.y += 0.25F;
            // Physics2D.OverlapCircleAll = внизу ігрока буде круг, який перевірятиме якщо існує в ньому інші колайдери
            Collider2D[] colliders = Physics2D.OverlapCircleAll(tr, 0.3F, LayerMask.GetMask("Ground"));



            isGrounded = colliders.Length > 0; // Більше 1 тому що в ньому завжди буде колайдер ігрока
            if (!isGrounded) State = CharacterState.Jump;
        }
       
    }


    public void AddOppositeForce() 
    {
        int oppositeDirection = 1; // щоб гравітація була в противоположну сторону
        if (direction.x > 0) oppositeDirection *= -1;

        rigidbody.velocity = Vector3.zero; // коли ігрок падає зверху на преграду, то його ускоренія більша, тому треба обнулити ускоренія
        rigidbody.AddForce(transform.up * 12.0F + transform.right * 2.0F * oppositeDirection, ForceMode2D.Impulse); // кидає у верх при ударі з преградою 
    }

    public override void ReceiveDemage(int damage_count = 1)
    {
        if (Time.time - LastRecievedDamageTime <= INTERVAL_DAMAGE) return;
        LastRecievedDamageTime = Time.time; // час у секундах з початку гри 

        PlayAudioDamage();

        Lives -= damage_count;
        State = CharacterState.Dizzy;

        if (Lives <= 0)
        {
            transform.position = respawn.transform.position;
            Lives = 5;
        }
        else
        {
            isGrounded = false;
            sprite.material.color = Color.red;
            AddOppositeForce();
        }


        //Debug.Log(lives);
    }

    private void OnTriggerEnter2D(Collider2D collider)
    {
        Bullet bullet = collider.GetComponent<Bullet>();

        if (bullet && bullet.Parent != gameObject)
        {
            ReceiveDemage();
        }
    }

    public void PlayAudioDie()
    {
        audioSource.clip = audioDie;
        audioSource.Play();
    }


    public void PlayAudioCongratulations()
    {
        audioSource.clip = audioCongratulations;
        audioSource.Play();
    }

    public void PlayAudioDamage()
    {
        audioSource.clip = audioDamage;
        audioSource.Play();
    }
}

