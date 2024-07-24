<?php
session_start();
include_once 'database.php';

// Enable error reporting for debugging
ini_set('display_errors', 1);
ini_set('display_startup_errors', 1);
error_reporting(E_ALL);

if (!isset($_SESSION['lecturer_id'])) {
    header("Location: login.php");
    exit;
}

$lecturer_name = htmlspecialchars($_SESSION['fullname']);

$database = new Database();
$db = $database->getConnection();

$query = "SELECT * FROM courses WHERE lecturer_id = :lecturer_id";
$stmt = $db->prepare($query);
$stmt->bindParam(':lecturer_id', $_SESSION['lecturer_id']);
$stmt->execute();
$courses = $stmt->fetchAll(PDO::FETCH_ASSOC);

$student = null; // Initialize $student

if ($_SERVER['REQUEST_METHOD'] === 'POST') {
    $course_id = $_POST['course_id'];

    // Check if course_id is valid
    if (empty($course_id)) {
        echo "Please select a course.";
    } else {
        $query = "SELECT students.* FROM students 
                  JOIN student_courses ON students.id = student_courses.student_id
                  WHERE student_courses.course_id = :course_id
                  ORDER BY RAND() LIMIT 1";
        $stmt = $db->prepare($query);
        $stmt->bindParam(':course_id', $course_id);
        $stmt->execute();
        $student = $stmt->fetch(PDO::FETCH_ASSOC);
    }
}
?>

<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>Choose Student</title>
    <link rel="stylesheet" href="CSS/main.css">
</head>
<body>
    <div class="sidebar">
        <h2>EduBridge</h2> 
        <ul>
            <li><a href="homepage.php"><i class="fas fa-home"></i> Dashboard</a></li>
            <li><a href="View_Students.php"><i class="fas fa-view"></i> View Students Data</a></li>
            <li><a href="student_update.php"><i class="fas fa-edit"></i> Update Students Data</a></li>
            <li><a href="phoneNum_search.php"><i class="fas fa-search"></i> Search by Phone Number</a></li>
            <li><a href="Create_Courses.php"><i class="fas fa-plus"></i> Manage Courses</a></li>
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
                <span class="name">Welcome, <?php echo $lecturer_name; ?></span>
                <a href="login.php">Logout</a>
            </div>
        </div>
        <div class="content">
            <div class="container">
                <h3>SELECT RANDOM STUDENT</h3>
                <form method="POST">
                    <select name="course_id">
                        <?php foreach ($courses as $course): ?>
                            <option value="<?= htmlspecialchars($course['id']) ?>"><?= htmlspecialchars($course['course_name']) ?></option>
                        <?php endforeach; ?>
                    </select>
                    <input type="submit" value="Choose Student">
                </form>

                <?php if ($student): ?>
                    <h3>Selected Student</h3>
                    <p>Matric No: <?= htmlspecialchars($student['matric_no']) ?></p>
                    <p>Full Name: <?= htmlspecialchars($student['fullname']) ?></p>
                    <p>Phone: <?= htmlspecialchars($student['phone']) ?></p>
                    <p>Program Code: <?= htmlspecialchars($student['program_code']) ?></p>
                <?php elseif ($_SERVER['REQUEST_METHOD'] === 'POST'): ?>
                    <p>No student found for the selected course.</p>
                <?php endif; ?>
            </div>
        </div>
    </div>
</body>
</html>
