USE [TPJ PROD]
GO
/****** Object:  StoredProcedure [hr].[GetPersonnelLeaveCredits]    Script Date: 09/01/21 6:52:32 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
ALTER PROCEDURE [hr].[GetPersonnelLeaveCredits](
	@ID BIGINT = NULL,
	@PersonnelID BIGINT = NULL,
	@LeaveTypeID INT = NULL,
	@YearValid INT = NULL
)
AS
BEGIN
	SELECT * 
	FROM [hr].[Personnel Leave Credits]
	WHERE (@ID IS NULL OR ID = @ID)
		AND (@PersonnelID IS NULL OR [Personnel ID] = @PersonnelID)
		AND (@LeaveTypeID IS NULL OR [Leave Type ID] = @LeaveTypeID)
		AND (@YearValid IS NULL OR [Year Valid] = @YearValid)
END