-- ============================================================================
-- SQL Schema v2 - SQLite
-- Thiết kế cơ sở dữ liệu hỗ trợ EAV đa danh mục và Phân quyền RBAC
-- Dự án: OmniRent
-- ============================================================================

PRAGMA foreign_keys = ON;

-- 1. Bảng User (Đã loại bỏ cột Role)
CREATE TABLE IF NOT EXISTS "User" (
    "Id" TEXT NOT NULL PRIMARY KEY,
    "Email" TEXT NOT NULL,
    "PasswordHash" TEXT NOT NULL,
    "FullName" TEXT NOT NULL,
    "Phone" TEXT NULL,
    "Role" TEXT NOT NULL DEFAULT 'RENTER', -- Legacy role for compatibility
    "AvatarUrl" TEXT NULL,
    "RenterTrustScore" REAL NOT NULL DEFAULT 80.0,
    "OwnerVerified" INTEGER NOT NULL DEFAULT 0, -- Boolean (0/1)
    "CreatedAt" INTEGER NOT NULL, -- Unix Milliseconds
    "UpdatedAt" INTEGER NOT NULL  -- Unix Milliseconds
);
CREATE UNIQUE INDEX IF NOT EXISTS "IX_User_Email" ON "User" ("Email");

-- 2. Bảng Role
CREATE TABLE IF NOT EXISTS "Role" (
    "Id" TEXT NOT NULL PRIMARY KEY,
    "Name" TEXT NOT NULL,
    "Description" TEXT NULL,
    "CreatedAt" INTEGER NOT NULL,
    "UpdatedAt" INTEGER NOT NULL
);
CREATE UNIQUE INDEX IF NOT EXISTS "IX_Role_Name" ON "Role" ("Name");

-- 3. Bảng Permission
CREATE TABLE IF NOT EXISTS "Permission" (
    "Id" TEXT NOT NULL PRIMARY KEY,
    "Name" TEXT NOT NULL,
    "Description" TEXT NULL,
    "CreatedAt" INTEGER NOT NULL,
    "UpdatedAt" INTEGER NOT NULL
);
CREATE UNIQUE INDEX IF NOT EXISTS "IX_Permission_Name" ON "Permission" ("Name");

-- 4. Bảng liên kết UserRole (N-N giữa User và Role)
CREATE TABLE IF NOT EXISTS "UserRole" (
    "UserId" TEXT NOT NULL,
    "RoleId" TEXT NOT NULL,
    PRIMARY KEY ("UserId", "RoleId"),
    FOREIGN KEY ("UserId") REFERENCES "User" ("Id") ON DELETE CASCADE,
    FOREIGN KEY ("RoleId") REFERENCES "Role" ("Id") ON DELETE CASCADE
);
CREATE INDEX IF NOT EXISTS "IX_UserRole_RoleId" ON "UserRole" ("RoleId");

-- 5. Bảng liên kết RolePermission (N-N giữa Role và Permission)
CREATE TABLE IF NOT EXISTS "RolePermission" (
    "RoleId" TEXT NOT NULL,
    "PermissionId" TEXT NOT NULL,
    PRIMARY KEY ("RoleId", "PermissionId"),
    FOREIGN KEY ("RoleId") REFERENCES "Role" ("Id") ON DELETE CASCADE,
    FOREIGN KEY ("PermissionId") REFERENCES "Permission" ("Id") ON DELETE CASCADE
);
CREATE INDEX IF NOT EXISTS "IX_RolePermission_PermissionId" ON "RolePermission" ("PermissionId");

-- 6. Bảng Category (Hệ thống phân cấp cha-con)
CREATE TABLE IF NOT EXISTS "Category" (
    "Id" TEXT NOT NULL PRIMARY KEY,
    "Name" TEXT NOT NULL,
    "Slug" TEXT NOT NULL,
    "ParentId" TEXT NULL,
    "CreatedAt" INTEGER NOT NULL,
    "UpdatedAt" INTEGER NOT NULL,
    FOREIGN KEY ("ParentId") REFERENCES "Category" ("Id") ON DELETE RESTRICT
);
CREATE UNIQUE INDEX IF NOT EXISTS "IX_Category_Slug" ON "Category" ("Slug");
CREATE INDEX IF NOT EXISTS "IX_Category_ParentId" ON "Category" ("ParentId");

-- 7. Bảng Attribute (Thuộc tính của danh mục cho mô hình EAV)
CREATE TABLE IF NOT EXISTS "Attribute" (
    "Id" TEXT NOT NULL PRIMARY KEY,
    "Name" TEXT NOT NULL,
    "Type" TEXT NOT NULL, -- TEXT, NUMBER, SELECT
    "Options" TEXT NULL, -- Danh sách lựa chọn cách nhau bởi dấu phẩy
    "CategoryId" TEXT NOT NULL,
    "CreatedAt" INTEGER NOT NULL,
    "UpdatedAt" INTEGER NOT NULL,
    FOREIGN KEY ("CategoryId") REFERENCES "Category" ("Id") ON DELETE RESTRICT
);
CREATE INDEX IF NOT EXISTS "IX_Attribute_CategoryId" ON "Attribute" ("CategoryId");

-- 8. Bảng Product (Đã loại bỏ cột CategoryId để hỗ trợ đa danh mục)
CREATE TABLE IF NOT EXISTS "Product" (
    "Id" TEXT NOT NULL PRIMARY KEY,
    "Name" TEXT NOT NULL,
    "Description" TEXT NOT NULL,
    "PricePerDay" REAL NOT NULL,
    "DepositAmount" REAL NOT NULL,
    "CategoryId" TEXT NOT NULL DEFAULT '', -- Legacy category for compatibility
    "OwnerId" TEXT NOT NULL,
    "ImagesJson" TEXT NOT NULL DEFAULT '[]',
    "Status" TEXT NOT NULL DEFAULT 'PENDING_APPROVAL', -- PENDING_APPROVAL, AVAILABLE, RENTED, MAINTENANCE
    "CreatedAt" INTEGER NOT NULL,
    "UpdatedAt" INTEGER NOT NULL,
    FOREIGN KEY ("CategoryId") REFERENCES "Category" ("Id") ON DELETE RESTRICT,
    FOREIGN KEY ("OwnerId") REFERENCES "User" ("Id") ON DELETE RESTRICT
);
CREATE INDEX IF NOT EXISTS "IX_Product_OwnerId" ON "Product" ("OwnerId");

-- 9. Bảng liên kết ProductCategory (N-N giữa Product và Category - Đa danh mục)
CREATE TABLE IF NOT EXISTS "ProductCategory" (
    "ProductId" TEXT NOT NULL,
    "CategoryId" TEXT NOT NULL,
    PRIMARY KEY ("ProductId", "CategoryId"),
    FOREIGN KEY ("ProductId") REFERENCES "Product" ("Id") ON DELETE CASCADE,
    FOREIGN KEY ("CategoryId") REFERENCES "Category" ("Id") ON DELETE CASCADE
);
CREATE INDEX IF NOT EXISTS "IX_ProductCategory_CategoryId" ON "ProductCategory" ("CategoryId");

-- 10. Bảng ProductAttribute (Giá trị thuộc tính cho sản phẩm - EAV Value)
CREATE TABLE IF NOT EXISTS "ProductAttribute" (
    "Id" TEXT NOT NULL PRIMARY KEY,
    "ProductId" TEXT NOT NULL,
    "AttributeId" TEXT NOT NULL,
    "Value" TEXT NOT NULL,
    "CreatedAt" INTEGER NOT NULL,
    "UpdatedAt" INTEGER NOT NULL,
    FOREIGN KEY ("ProductId") REFERENCES "Product" ("Id") ON DELETE CASCADE,
    FOREIGN KEY ("AttributeId") REFERENCES "Attribute" ("Id") ON DELETE RESTRICT
);
CREATE UNIQUE INDEX IF NOT EXISTS "IX_ProductAttribute_ProductId_AttributeId" ON "ProductAttribute" ("ProductId", "AttributeId");
CREATE INDEX IF NOT EXISTS "IX_ProductAttribute_AttributeId" ON "ProductAttribute" ("AttributeId");

-- 11. Bảng Booking
CREATE TABLE IF NOT EXISTS "Booking" (
    "Id" TEXT NOT NULL PRIMARY KEY,
    "ProductId" TEXT NOT NULL,
    "RenterId" TEXT NOT NULL,
    "StartDate" INTEGER NOT NULL,
    "EndDate" INTEGER NOT NULL,
    "TotalPrice" REAL NOT NULL,
    "DepositPaid" INTEGER NOT NULL DEFAULT 0,
    "Status" TEXT NOT NULL DEFAULT 'PENDING', -- PENDING, APPROVED, ONGOING, COMPLETED, REJECTED, CANCELLED
    "PaymentStatus" TEXT NOT NULL DEFAULT 'UNPAID', -- UNPAID, PAID, REFUNDED
    "CreatedAt" INTEGER NOT NULL,
    "UpdatedAt" INTEGER NOT NULL,
    FOREIGN KEY ("ProductId") REFERENCES "Product" ("Id") ON DELETE CASCADE,
    FOREIGN KEY ("RenterId") REFERENCES "User" ("Id") ON DELETE RESTRICT
);
CREATE INDEX IF NOT EXISTS "IX_Booking_ProductId" ON "Booking" ("ProductId");
CREATE INDEX IF NOT EXISTS "IX_Booking_RenterId" ON "Booking" ("RenterId");

-- 12. Bảng Review
CREATE TABLE IF NOT EXISTS "Review" (
    "Id" TEXT NOT NULL PRIMARY KEY,
    "BookingId" TEXT NOT NULL,
    "ProductId" TEXT NOT NULL,
    "UserId" TEXT NOT NULL,
    "Rating" INTEGER NOT NULL,
    "Comment" TEXT NOT NULL,
    "CreatedAt" INTEGER NOT NULL,
    FOREIGN KEY ("BookingId") REFERENCES "Booking" ("Id") ON DELETE CASCADE,
    FOREIGN KEY ("ProductId") REFERENCES "Product" ("Id") ON DELETE CASCADE,
    FOREIGN KEY ("UserId") REFERENCES "User" ("Id") ON DELETE RESTRICT
);
CREATE INDEX IF NOT EXISTS "IX_Review_BookingId" ON "Review" ("BookingId");
CREATE INDEX IF NOT EXISTS "IX_Review_ProductId" ON "Review" ("ProductId");
CREATE INDEX IF NOT EXISTS "IX_Review_UserId" ON "Review" ("UserId");

-- 13. Bảng MaintenanceLog
CREATE TABLE IF NOT EXISTS "MaintenanceLog" (
    "Id" TEXT NOT NULL PRIMARY KEY,
    "ProductId" TEXT NOT NULL,
    "OwnerId" TEXT NOT NULL,
    "IssueDescription" TEXT NOT NULL,
    "Cost" REAL NOT NULL,
    "StartDate" INTEGER NOT NULL,
    "EndDate" INTEGER NULL,
    "Status" TEXT NOT NULL DEFAULT 'UNDER_REPAIR', -- UNDER_REPAIR, RESOLVED
    "CreatedAt" INTEGER NOT NULL,
    FOREIGN KEY ("ProductId") REFERENCES "Product" ("Id") ON DELETE CASCADE,
    FOREIGN KEY ("OwnerId") REFERENCES "User" ("Id") ON DELETE RESTRICT
);
CREATE INDEX IF NOT EXISTS "IX_MaintenanceLog_ProductId" ON "MaintenanceLog" ("ProductId");
CREATE INDEX IF NOT EXISTS "IX_MaintenanceLog_OwnerId" ON "MaintenanceLog" ("OwnerId");

-- 14. Bảng Message (Chat giữa Renter và Owner)
CREATE TABLE IF NOT EXISTS "Message" (
    "Id" TEXT NOT NULL PRIMARY KEY,
    "SenderId" TEXT NOT NULL,
    "ReceiverId" TEXT NOT NULL,
    "Content" TEXT NOT NULL,
    "CreatedAt" INTEGER NOT NULL,
    FOREIGN KEY ("SenderId") REFERENCES "User" ("Id") ON DELETE RESTRICT,
    FOREIGN KEY ("ReceiverId") REFERENCES "User" ("Id") ON DELETE RESTRICT
);
CREATE INDEX IF NOT EXISTS "IX_Message_SenderId_ReceiverId" ON "Message" ("SenderId", "ReceiverId");

-- 15. Bảng Notification
CREATE TABLE IF NOT EXISTS "Notification" (
    "Id" TEXT NOT NULL PRIMARY KEY,
    "UserId" TEXT NOT NULL,
    "Title" TEXT NOT NULL,
    "Content" TEXT NOT NULL,
    "IsRead" INTEGER NOT NULL DEFAULT 0,
    "CreatedAt" INTEGER NOT NULL,
    FOREIGN KEY ("UserId") REFERENCES "User" ("Id") ON DELETE CASCADE
);
CREATE INDEX IF NOT EXISTS "IX_Notification_UserId" ON "Notification" ("UserId");

-- 16. Bảng QrCheckIn
CREATE TABLE IF NOT EXISTS "QrCheckIn" (
    "Id" TEXT NOT NULL PRIMARY KEY,
    "BookingId" TEXT NOT NULL,
    "QrCodeString" TEXT NOT NULL,
    "CheckInType" TEXT NOT NULL, -- CHECKIN, CHECKOUT
    "ScannedAt" INTEGER NOT NULL,
    FOREIGN KEY ("BookingId") REFERENCES "Booking" ("Id") ON DELETE CASCADE
);
CREATE INDEX IF NOT EXISTS "IX_QrCheckIn_BookingId" ON "QrCheckIn" ("BookingId");

-- 17. Bảng DamageReport
CREATE TABLE IF NOT EXISTS "DamageReport" (
    "Id" TEXT NOT NULL PRIMARY KEY,
    "BookingId" TEXT NOT NULL,
    "ImageUrl" TEXT NOT NULL,
    "Severity" TEXT NOT NULL, -- NONE, LIGHT, SEVERE
    "Details" TEXT NOT NULL,
    "RepairEstimate" REAL NOT NULL DEFAULT 0.0,
    "CreatedAt" INTEGER NOT NULL,
    FOREIGN KEY ("BookingId") REFERENCES "Booking" ("Id") ON DELETE CASCADE
);
CREATE INDEX IF NOT EXISTS "IX_DamageReport_BookingId" ON "DamageReport" ("BookingId");
