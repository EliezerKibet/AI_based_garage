<?php
session_start();
if (!isset($_SESSION['lecturer_id'])) {
    header("Location: login.php");
    exit;
}
?>

<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>Lecturer Dashboard</title>
    <link rel="stylesheet" href="style.css">
    
    <link rel="stylesheet" href="https://cdnjs.cloudflare.com/ajax/libs/font-awesome/5.15.3/css/all.min.css">
</head>
<body style="background: url('../images/background.jpg') no-repeat center center fixed; background-size: cover;">
    <div class="sidebar">
        <h2>EduBridge</h2>
        <ul>
           <li><a href="Homepage.php"><i class="fas fa-home"></i> Dashboard</a></li>
            <li><a href="view_students.php"><i class="fas fa-user"></i> Manage Students</a></li>
            <li><a href="create_courses.php"><i class="fas fa-book"></i> Manage Courses</a></li>
            <li><a href="login.php"><i class="fas fa-sign-out-alt"></i> Logout</a></li>
        </ul>
    </div>
    <div class="main-content">
