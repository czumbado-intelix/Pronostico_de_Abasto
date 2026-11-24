export const onRequestGet: PagesFunction = async ({ request }) => {
  const email = request.headers.get('Cf-Access-Authenticated-User-Email')?.trim() ?? ''

  return Response.json({
    email,
  })
}
