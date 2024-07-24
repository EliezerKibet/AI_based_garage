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

if ($_SERVER['REQUEST_METHOD'] === 'POST') {
    $phone = "%" . $_POST['phone'] . "%"; // Use LIKE syntax for partial matching

    $query = "SELECT * FROM students WHERE phone LIKE :phone";
    $stmt = $db->prepare($query);
    $stmt->bindParam(':phone', $phone);
    $stmt->execute();
    $students = $stmt->fetchAll(PDO::FETCH_ASSOC); // Fetch all matching rows
}
?>

<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>Search by Phone Number</title>
    <link rel="stylesheet" href="CSS/main.css">
</head>
<body>
    <div class="sidebar">
        <h2>EduBridge</h2> 
        <ul>
            <li><a href="homepage.php"><i class="fas fa-home"></i> Dashboard</a></li>
            <li><a href="View_Students.php"><i class="fas fa-view"></i> View Students Data</a></li>
            <li><a href="student_update.php"><i class="fas fa-edit"></i> Update Student Data</a></li>
            <li><a href="rand_student.php"><i class="fas fa-random"></i> Choose Random Student</a></li>
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
                <span class="name">Welcome, <?php echo htmlspecialchars($lecturer_name); ?></span>
                <a href="login.php">Logout</a>
            </div>
        </div>
        <div class="content">
            <div class="container">
                <h3>SEARCH STUDENT BY PHONE NUMBER</h3>
                <form method="POST">
                    <input type="text" name="phone" placeholder="Enter phone number or part of it" required>
                    <input type="submit" value="Search"> 
                </form>

                <?php if (isset($students) && count($students) > 0): ?>
                    <h3>Student Details</h3>
                    <table>
                        <tr>
                            <th>Matric No</th>
                            <th>Full Name</th>
                            <th>Phone</th>
                            <th>Program Code</th>
                        </tr>
                        <?php foreach ($students as $student): ?>
                        <tr>
                            <td><?= htmlspecialchars($student['matric_no']) ?></td>
                            <td><?= htmlspecialchars($student['fullname']) ?></td>
                            <td><?= htmlspecialchars($student['phone']) ?></td>
                            <td><?= htmlspecialchars($student['program_code']) ?></td>
                        </tr>
                        <?php endforeach; ?>
                    </table>
                <?php elseif ($_SERVER['REQUEST_METHOD'] === 'POST'): ?>
                    <p>No student found with the given phone number.</p>
                <?php endif; ?>
            </div>
        </div>
    </div>
</body>
</html>
