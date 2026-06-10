-- ============================================================================
-- SQL Migration Script (V1 -> V2) - SQLite
-- Chuyển đổi dữ liệu sang mô hình EAV đa danh mục và Phân quyền RBAC
-- Dự án: OmniRent
-- ============================================================================

PRAGMA foreign_keys = OFF;

-- 1. Tạo các bảng mới nếu chưa tồn tại

-- Bảng Role (Vai trò)
CREATE TABLE IF NOT EXISTS "Role" (
    "Id" TEXT NOT NULL PRIMARY KEY,
    "Name" TEXT NOT NULL,
    "Description" TEXT NULL,
    "CreatedAt" INTEGER NOT NULL,
    "UpdatedAt" INTEGER NOT NULL
);
CREATE UNIQUE INDEX IF NOT EXISTS "IX_Role_Name" ON "Role" ("Name");

-- Bảng Permission (Quyền)
CREATE TABLE IF NOT EXISTS "Permission" (
    "Id" TEXT NOT NULL PRIMARY KEY,
    "Name" TEXT NOT NULL,
    "Description" TEXT NULL,
    "CreatedAt" INTEGER NOT NULL,
    "UpdatedAt" INTEGER NOT NULL
);
CREATE UNIQUE INDEX IF NOT EXISTS "IX_Permission_Name" ON "Permission" ("Name");

-- Bảng liên kết UserRole (Vai trò của người dùng)
CREATE TABLE IF NOT EXISTS "UserRole" (
    "UserId" TEXT NOT NULL,
    "RoleId" TEXT NOT NULL,
    PRIMARY KEY ("UserId", "RoleId"),
    FOREIGN KEY ("UserId") REFERENCES "User" ("Id") ON DELETE CASCADE,
    FOREIGN KEY ("RoleId") REFERENCES "Role" ("Id") ON DELETE CASCADE
);
CREATE INDEX IF NOT EXISTS "IX_UserRole_RoleId" ON "UserRole" ("RoleId");

-- Bảng liên kết RolePermission (Quyền của vai trò)
CREATE TABLE IF NOT EXISTS "RolePermission" (
    "RoleId" TEXT NOT NULL,
    "PermissionId" TEXT NOT NULL,
    PRIMARY KEY ("RoleId", "PermissionId"),
    FOREIGN KEY ("RoleId") REFERENCES "Role" ("Id") ON DELETE CASCADE,
    FOREIGN KEY ("PermissionId") REFERENCES "Permission" ("Id") ON DELETE CASCADE
);
CREATE INDEX IF NOT EXISTS "IX_RolePermission_PermissionId" ON "RolePermission" ("PermissionId");

-- Bảng liên kết ProductCategory (Đa danh mục cho sản phẩm)
CREATE TABLE IF NOT EXISTS "ProductCategory" (
    "ProductId" TEXT NOT NULL,
    "CategoryId" TEXT NOT NULL,
    PRIMARY KEY ("ProductId", "CategoryId"),
    FOREIGN KEY ("ProductId") REFERENCES "Product" ("Id") ON DELETE CASCADE,
    FOREIGN KEY ("CategoryId") REFERENCES "Category" ("Id") ON DELETE CASCADE
);
CREATE INDEX IF NOT EXISTS "IX_ProductCategory_CategoryId" ON "ProductCategory" ("CategoryId");


-- 2. Seed dữ liệu Vai trò và Quyền mặc định

-- Khởi tạo Vai trò
INSERT INTO "Role" ("Id", "Name", "Description", "CreatedAt", "UpdatedAt")
VALUES
('role-admin-uuid-0000-0000-000000000001', 'ADMIN', 'Quản trị viên hệ thống', 1718010000000, 1718010000000),
('role-owner-uuid-0000-0000-000000000002', 'OWNER', 'Chủ sở hữu tài sản cho thuê', 1718010000000, 1718010000000),
('role-renter-uuid-0000-0000-000000000003', 'RENTER', 'Khách thuê đồ', 1718010000000, 1718010000000)
ON CONFLICT("Name") DO NOTHING;

-- Khởi tạo Quyền hạn
INSERT INTO "Permission" ("Id", "Name", "Description", "CreatedAt", "UpdatedAt")
VALUES
('perm-read-products', 'READ_PRODUCTS', 'Xem danh sách sản phẩm', 1718010000000, 1718010000000),
('perm-create-product', 'CREATE_PRODUCT', 'Đăng tin sản phẩm mới', 1718010000000, 1718010000000),
('perm-update-product', 'UPDATE_PRODUCT', 'Chỉnh sửa sản phẩm cá nhân', 1718010000000, 1718010000000),
('perm-delete-product', 'DELETE_PRODUCT', 'Xóa sản phẩm cá nhân', 1718010000000, 1718010000000),
('perm-manage-users', 'MANAGE_USERS', 'Quản lý toàn bộ người dùng', 1718010000000, 1718010000000),
('perm-manage-categories', 'MANAGE_CATEGORIES', 'Quản lý danh mục và thuộc tính EAV', 1718010000000, 1718010000000),
('perm-book-product', 'BOOK_PRODUCT', 'Đặt thuê sản phẩm', 1718010000000, 1718010000000),
('perm-approve-booking', 'APPROVE_BOOKING', 'Duyệt yêu cầu thuê sản phẩm', 1718010000000, 1718010000000)
ON CONFLICT("Name") DO NOTHING;

-- Gán quyền cho các vai trò (RolePermission)
-- ADMIN có tất cả các quyền
INSERT INTO "RolePermission" ("RoleId", "PermissionId")
SELECT 'role-admin-uuid-0000-0000-000000000001', "Id" FROM "Permission"
ON CONFLICT DO NOTHING;

-- OWNER có các quyền đăng bài, quản lý sản phẩm cá nhân và duyệt booking
INSERT INTO "RolePermission" ("RoleId", "PermissionId")
VALUES
('role-owner-uuid-0000-0000-000000000002', 'perm-read-products'),
('role-owner-uuid-0000-0000-000000000002', 'perm-create-product'),
('role-owner-uuid-0000-0000-000000000002', 'perm-update-product'),
('role-owner-uuid-0000-0000-000000000002', 'perm-delete-product'),
('role-owner-uuid-0000-0000-000000000002', 'perm-approve-booking')
ON CONFLICT DO NOTHING;

-- RENTER có quyền xem và đặt thuê sản phẩm
INSERT INTO "RolePermission" ("RoleId", "PermissionId")
VALUES
('role-renter-uuid-0000-0000-000000000003', 'perm-read-products'),
('role-renter-uuid-0000-0000-000000000003', 'perm-book-product')
ON CONFLICT DO NOTHING;


-- 3. Di chuyển dữ liệu cũ (Data Migration)

-- Di chuyển liên kết Sản phẩm - Danh mục sang bảng ProductCategory
INSERT INTO "ProductCategory" ("ProductId", "CategoryId")
SELECT "Id", "CategoryId" FROM "Product" 
WHERE "CategoryId" IS NOT NULL AND "CategoryId" != ''
ON CONFLICT DO NOTHING;

-- Di chuyển vai trò của người dùng hiện có sang bảng UserRole
INSERT INTO "UserRole" ("UserId", "RoleId")
SELECT u."Id", r."Id"
FROM "User" u
JOIN "Role" r ON u."Role" = r."Name"
ON CONFLICT DO NOTHING;


-- 4. Giữ lại các cột cũ làm dự phòng cho tương thích ngược (Backward Compatibility)
-- Không thực hiện DROP COLUMN để tránh lỗi biên dịch và lỗi chạy truy vấn trên các chức năng cũ.
-- Cột "CategoryId" trong Product và "Role" trong User vẫn tồn tại song song với hệ thống mới.


PRAGMA foreign_keys = ON;
