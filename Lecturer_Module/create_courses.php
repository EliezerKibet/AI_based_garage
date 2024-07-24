 <?php
session_start();
include_once 'database.php';
 
if (!isset($_SESSION['lecturer_id'])) {
    header("Location: login.php");
    exit;
}
 
$lecturer_name = $_SESSION['fullname'];
 
$message = ''; // Initialize message variable
 
if ($_SERVER['REQUEST_METHOD'] === 'POST') {
    $course_code = $_POST['course_code'];
    $course_name = $_POST['course_name'];
    $group_number = $_POST['group_number'];
 
    // Validate course code format
    if (!preg_match('/^[A-Z]{3} \d{4}$/', $course_code)) {
        $message = "Invalid course code format. Please enter in the format 'ABC 1234'.";
    } else {
        // Using PDO
        $database = new Database();
        $db = $database->getConnection();
 
        // Check if the course with the same code, name, and group number already exists
        $checkQuery = "SELECT * FROM courses WHERE course_code = :course_code AND course_name = :course_name AND group_number = :group_number";
        $checkStmt = $db->prepare($checkQuery);
        $checkStmt->bindParam(':course_code', $course_code);
        $checkStmt->bindParam(':course_name', $course_name);
        $checkStmt->bindParam(':group_number', $group_number);
        $checkStmt->execute();
 
        if ($checkStmt->rowCount() > 0) {
            $message = "Group number is already taken. Please choose a different group number.";
        } else {
            // Insert new course if group number is unique
            $query = "INSERT INTO courses (course_code, course_name, lecturer_id, group_number) VALUES (:course_code, :course_name, :lecturer_id, :group_number)";
            $stmt = $db->prepare($query);
            $stmt->bindParam(':course_code', $course_code);
            $stmt->bindParam(':course_name', $course_name);
            $stmt->bindParam(':lecturer_id', $_SESSION['lecturer_id']);
            $stmt->bindParam(':group_number', $group_number);
 
            if ($stmt->execute()) {
                $message = "Course created successfully.";
            } else {
                $message = "Failed to create course.";
            }
        }
    }
}
?>
 
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>Create Course</title>
    <link rel="stylesheet" href="CSS/main.css">
    <script>
        function validateForm() {
            var courseCode = document.forms["courseForm"]["course_code"].value;
            var regex = /^[A-Z]{3} \d{4}$/;
            if (!regex.test(courseCode)) {
                alert("Invalid course code format. Please enter in the format 'ABC 1234'.");
                return false;
            }
            return true;
        }
    </script>
</head>
<body>
    <div class="sidebar">
        <h2>EduBridge</h2>
        <ul>
            <li><a href="homepage.php"><i class="fas fa-home"></i> Dashboard</a></li>
            <li><a href="Drop_Courses.php"><i class="fas fa-edit"></i> Drop Courses</a></li>
            <li><a href="View_Students.php"><i class="fas fa-random"></i> Manage Students</a></li>
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
                <a href="logout.php">Logout</a>
            </div>
        </div>
        <div class="content">
            <div class="container">
                <form name="courseForm" method="POST" onsubmit="return validateForm()">
                    <h2>CREATE COURSES</h2>
                    <input type="text" name="course_code" placeholder="Course Code" required>
                    <input type="text" name="course_name" placeholder="Course Name" required>
                    
                    <?php
                    // Fetch existing group numbers for the given course code and name
                    $groupNumbers = [];
                    if ($_SERVER['REQUEST_METHOD'] !== 'POST' || (isset($course_code) && isset($course_name))) {
                        $database = new Database();
                        $db = $database->getConnection();
                        $groupQuery = "SELECT DISTINCT group_number FROM courses WHERE course_code = :course_code AND course_name = :course_name";
                        $groupStmt = $db->prepare($groupQuery);
                        $groupStmt->bindParam(':course_code', $course_code);
                        $groupStmt->bindParam(':course_name', $course_name);
                        $groupStmt->execute();
                        
                        $groupNumbers = $groupStmt->fetchAll(PDO::FETCH_COLUMN);
                    }
                    ?>
 
                    <select name="group_number" required>
                        <option value="" disabled selected>Select Group Number</option>
                        <?php foreach (range(1, 10) as $number): ?>
                            <?php if (!in_array($number, $groupNumbers)): ?>
                                <option value="<?php echo $number; ?>"><?php echo $number; ?></option>
                            <?php endif; ?>
                        <?php endforeach; ?>
                    </select>
 
                    <input type="submit" value="Create Course">
                </form>
                <?php if ($message): ?>
                    <p><?php echo htmlspecialchars($message); ?></p>
                <?php endif; ?>
            </div>
        </div>
    </div>
</body>
</html>
 
