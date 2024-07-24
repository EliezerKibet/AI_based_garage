<?php
session_start();
include_once 'database.php';

if ($_SERVER['REQUEST_METHOD'] === 'POST') {
    $username = $_POST['username'];
    $password = $_POST['password'];

    // Using PDO
    $database = new Database();
    $db = $database->getConnection();

    $query = "SELECT * FROM lecturers WHERE username = :username";
    $stmt = $db->prepare($query);
    $stmt->bindParam(':username', $username);
    $stmt->execute();

    $user = $stmt->fetch(PDO::FETCH_ASSOC);

    if ($user && password_verify($password, $user['password'])) {
        $_SESSION['lecturer_id'] = $user['id'];
        $_SESSION['fullname'] = $user['fullname'];
        header("Location: homepage.php");
    } else {
        $error_message = "Invalid username or password.";
    }
}
?>

<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>Login</title>
    <link rel="stylesheet" href="CSS/style.css">
	
	<style>
        .container img {
            width: 150px; /* Adjust the width as needed */
            height: auto; /* Maintains the aspect ratio */
            display: block;
            margin: 0 auto; /* Centers the image */
			opacity: 0.7; 
        }
    </style>
</head>
<body>
    <div class="container">
        <img src="../images/logo.png" alt="Logo">
        <h3>Hello! lets get started </h3>
        <p>Sign in to continue.</p>
        <?php if (isset($error_message)): ?>
            <div class="error"><?= $error_message ?></div>
        <?php endif; ?>
        <form method="POST">
            <input type="text" name="username" placeholder="Enter your username" required>
            <input type="password" name="password" placeholder="Enter your password" required>
            <input type="submit" value="Login">
        </form>
        <a href="lecturer_registration.php">Register</a>
        <a href="../Student_Module/student_registration.php">Enter Student’s Module</a>
    </div>
</body>
</html>
