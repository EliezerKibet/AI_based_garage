<?php
class Database {
    private $host = 'localhost';
    private $db_name = 'eduauthorities';
    private $username = 'root';
    private $password = '';
    public $conn;

    public function getConnection() {
        $this->conn = null;
        try {
            $this->conn = new PDO("mysql:host=" . $this->host . ";dbname=" . $this->db_name, $this->username, $this->password);
            $this->conn->exec("set names utf8");
        } catch(PDOException $exception) {
            echo "Connection error: " . $exception->getMessage();
        }
        return $this->conn;
    }
}

$message = ''; // Initialize message variable
$message_type = ''; // Initialize message type variable

if ($_SERVER['REQUEST_METHOD'] === 'POST') {
    $matric_no = $_POST['matric_no'];
    $fullname = $_POST['fullname'];
    $phone = $_POST['phone'];
    $program_code = $_POST['program_code'];
    $selected_courses = isset($_POST['course_ids']) ? $_POST['course_ids'] : []; // Array of selected course codes

    // Validate phone number format
    if (!preg_match('/^0\d{9,10}$/', $phone)) {
        $message = "Invalid phone number. Please enter a valid phone number.";
        $message_type = 'error';
    } else {
        // Using mysqli object-oriented
        $conn = new mysqli("localhost", "root", "", "eduauthorities");

        if ($conn->connect_error) {
            die("Connection failed: " . $conn->connect_error);
        }

        // Check if matric number or phone number already exists
        $stmt = $conn->prepare("SELECT * FROM students WHERE matric_no = ? OR phone = ?");
        $stmt->bind_param("ss", $matric_no, $phone);
        $stmt->execute();
        $result = $stmt->get_result();

        if ($result->num_rows > 0) {
            $message = "Matric number or phone number already exists. ";
            $message_type = 'error';
        } else {
            // Insert student details
            $stmt = $conn->prepare("INSERT INTO students (matric_no, fullname, phone, program_code) VALUES (?, ?, ?, ?)");
            $stmt->bind_param("ssss", $matric_no, $fullname, $phone, $program_code);

            if ($stmt->execute()) {
                $student_id = $stmt->insert_id;

                // Insert student course enrollments
                foreach ($selected_courses as $course_code) {
                    // Get available groups for the selected course
                    $stmt = $conn->prepare("SELECT id, group_number FROM courses WHERE course_code = ?");
                    $stmt->bind_param("s", $course_code);
                    $stmt->execute();
                    $course_result = $stmt->get_result();
                    $group_options = [];

                    while ($row = $course_result->fetch_assoc()) {
                        $group_options[] = [
                            'id' => $row['id'],
                            'group_number' => $row['group_number']
                        ];
                    }

                    // Randomly assign to one of the available groups
                    $random_group = $group_options[array_rand($group_options)];

                    // Insert enrollment
                    $stmt = $conn->prepare("INSERT INTO student_courses (student_id, course_id) VALUES (?, ?)");
                    $stmt->bind_param("ii", $student_id, $random_group['id']);
                    $stmt->execute();
                }

                $message = "Registration successful.";
                $message_type = 'success';
            } else {
                $message = "Registration failed.";
                $message_type = 'error';
            }
        }

        $stmt->close();
        $conn->close();
    }
}

// Fetch available grouped courses for selection
$database = new Database();
$db = $database->getConnection();

$query = "
    SELECT course_code, course_name, GROUP_CONCAT(DISTINCT id ORDER BY group_number) as group_ids
    FROM courses
    GROUP BY course_code, course_name
";
$stmt = $db->prepare($query);
$stmt->execute();
$grouped_courses = $stmt->fetchAll(PDO::FETCH_ASSOC);
?>

<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>Student Registration</title>
    <link rel="stylesheet" href="student_registration.css">
</head>
<body>
    <div class="container">
        <img src="../images/logo.png" alt="Logo">
        <h1>Student Registration</h1>
        <form method="POST">
            <div class="form-group">
                <label for="matric_no">Matric No</label>
                <input type="text" name="matric_no" id="matric_no" required>
            </div>
            <div class="form-group">
                <label for="fullname">Full Name</label>
                <input type="text" name="fullname" id="fullname" required>
            </div>
            <div class="form-group">
                <label for="phone">Phone</label>
                <input type="text" name="phone" id="phone" required>
            </div>
            <div class="form-group">
                <label for="program_code">Program</label>
                <select name="program_code" id="program_code" required>
                    <option value="IT301">Diploma in Information Technology</option>
                    <option value="IT302">Diploma Computer Science (Industrial Computing)</option>
                    <option value="IT401">Bachelor in Information Technology (Hons)</option>
                    <option value="IT402">Bachelor of Software Engineering (Hons)</option>
                    <option value="IT403">Bachelor of Computer Science (Hons)</option>
                    <option value="IT405">Bachelor of Multimedia Industry (Hons)</option>
                    <option value="BT402">Bachelor of Bioinformatics (Hons)</option>
                </select>
            </div>
            <div class="checkbox-group">
                <label>Select Courses:</label>
            </div>
            <div>
                <?php foreach ($grouped_courses as $course): ?>
                    <div class="checkbox-item">
                        <input type="checkbox" name="course_ids[]" value="<?= $course['course_code'] ?>" id="course_<?= $course['course_code'] ?>">
                        <label for="course_<?= $course['course_code'] ?>"><?= $course['course_name'] ?></label>
                    </div>
                <?php endforeach; ?>
            </div>
            <input type="submit" value="Register">
            <a href="../Lecturer_Module/login.php">Go Back</a>
        </form>
        <?php if ($message): ?>
            <p class="message <?= $message_type ?>"><?php echo htmlspecialchars($message); ?></p>
        <?php endif; ?>
    </div>
</body>
</html>
