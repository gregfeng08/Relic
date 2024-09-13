using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    private Rigidbody rb;
    [SerializeField] float speed;
    [SerializeField] float jumpHeight;
    [SerializeField] float groundCheckDistance;
    [SerializeField] LayerMask groundLayer;

    private bool isGrounded;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
    }

    void Update()
    {
        float horizontalInput = Input.GetAxis("Horizontal");
        float verticalInput = Input.GetAxis("Vertical");

        // Calculate the movement direction based on the player's orientation
        Vector3 forwardMovement = transform.forward * verticalInput;
        Vector3 rightMovement = transform.right * horizontalInput;

        // Combine the forward and right movements
        Vector3 movement = forwardMovement + rightMovement;

        // Apply force to move the Rigidbody
        rb.AddForce(movement * speed);
    }

    /*void CheckGroundStatus()
    {
        // Cast a ray downward to check if the player is grounded
        RaycastHit hit;
        if (Physics.Raycast(transform.position, Vector3.down, out hit, groundCheckDistance, groundLayer))
        {
            isGrounded = true;
        }
        else
        {
            isGrounded = false;
        }
    }*/
}
