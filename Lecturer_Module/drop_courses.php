<?php
session_start();
include_once 'database.php';

if (!isset($_SESSION['lecturer_id'])) {
    header("Location: login.php");
    exit;
}

$lecturer_name = $_SESSION['fullname'];

$database = new Database();
$db = $database->getConnection();

$query = "SELECT * FROM courses WHERE lecturer_id = :lecturer_id";
$stmt = $db->prepare($query);
$stmt->bindParam(':lecturer_id', $_SESSION['lecturer_id']);
$stmt->execute();
$courses = $stmt->fetchAll(PDO::FETCH_ASSOC);

$message = ''; // Initialize message variable

if ($_SERVER['REQUEST_METHOD'] === 'POST') {
    $course_id = $_POST['course_id'];

    // Using mysqli procedural
    $conn = mysqli_connect("localhost", "root", "", "eduauthorities");
    if (!$conn) {
        die("Connection failed: " . mysqli_connect_error());
    }

    $stmt = mysqli_prepare($conn, "DELETE FROM courses WHERE id = ? AND lecturer_id = ?");
    mysqli_stmt_bind_param($stmt, "ii", $course_id, $_SESSION['lecturer_id']);
    $result = mysqli_stmt_execute($stmt);

    if ($result) {
        $message = "Course dropped successfully.";
    } else {
        $message = "Course drop failed.";
    }

    mysqli_stmt_close($stmt);
    mysqli_close($conn);
}
?>

<!DOCTYPE html> 
<html lang="en">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>Drop Course</title>
    <link rel="stylesheet" href="CSS/main.css">
</head>
<body>
    <div class="sidebar">
        <h2>EduBridge</h2>
        <ul>
            <li><a href="homepage.php"><i class="fas fa-home"></i> Dashboard</a></li>
            <li><a href="create_courses.php"><i class="fas fa-database"></i> Create Courses</a></li>
            <li><a href="view_students.php"><i class="fas fa-random"></i> Manage Students</a></li>
            <li><a href="login.php"><i class="fas fa-sign-out-alt"></i> Logout</a></li>
        </ul>
    </div>
    <div class="main-content">
        <div class="header">
            <div class="logo">
                <img src="../images/logo.png" alt="Logo" style="width: 100px; height: auto;">
            </div>
            <div class="user-info">
                <img src="../images/user-icon.png" alt="User Icon">
                <span class="name">Welcome, <?php echo htmlspecialchars($lecturer_name); ?></span>
                <a href="login.php">Logout</a>
            </div>
        </div>
        <div class="content">
            <div class="container">
                <h1>DROP COURSES</h1>
                <?php if (empty($courses)): ?>
                    <p>No courses available to drop.</p>
                <?php else: ?>
                    <form method="POST">
                        <select name="course_id">
                            <?php foreach ($courses as $course): ?>
                                <option value="<?= htmlspecialchars($course['id']) ?>"><?= htmlspecialchars($course['course_name']) ?></option>
                            <?php endforeach; ?>
                        </select>
                        <input type="submit" value="Drop Course">
                    </form>
                <?php endif; ?>
                <?php if ($message): ?>
                    <p><?php echo htmlspecialchars($message); ?></p>
                <?php endif; ?>
            </div>
        </div>
    </div>
</body>
</html>
