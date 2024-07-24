<?php
include_once 'database.php';

$message = ''; // Initialize message variable

if ($_SERVER['REQUEST_METHOD'] === 'POST') {
    $username = $_POST['username'];
    $password = password_hash($_POST['password'], PASSWORD_BCRYPT);
    $fullname = $_POST['fullname'];

    // Using mysqli procedural
    $conn = mysqli_connect("localhost", "root", "", "eduauthorities");
    if (!$conn) {
        die("Connection failed: " . mysqli_connect_error());
    }

    // Check if username already exists
    $stmt = mysqli_prepare($conn, "SELECT username FROM lecturers WHERE username = ?");
    mysqli_stmt_bind_param($stmt, "s", $username);
    mysqli_stmt_execute($stmt);
    mysqli_stmt_store_result($stmt);

    if (mysqli_stmt_num_rows($stmt) > 0) {
        // Username already exists
        $message = "Username already exists.";
    } else {
        // Insert new lecturer
        $stmt = mysqli_prepare($conn, "INSERT INTO lecturers (username, password, fullname) VALUES (?, ?, ?)");
        mysqli_stmt_bind_param($stmt, "sss", $username, $password, $fullname);
        $result = mysqli_stmt_execute($stmt);

        if ($result) {
            $message = "Registration successful.";
        } else {
            $message = "Registration failed.";
        }

        mysqli_stmt_close($stmt);
    }

    mysqli_close($conn);
}
?>

<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>Register</title>
    <link rel="stylesheet" href="style.css">
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
        <h1>Register</h1>
        <form method="POST">
            <input type="text" name="username" placeholder="Enter your username" required>
            <input type="password" name="password" placeholder="Enter your password" required>
            <input type="text" name="fullname" placeholder="Enter your full name" required>
            <input type="submit" value="Register">
        </form>
        <a href="login.php">Back to Login</a>
        <?php if ($message): ?>
            <p><?php echo htmlspecialchars($message); ?></p>
        <?php endif; ?>
    </div>
</body>
</html>
