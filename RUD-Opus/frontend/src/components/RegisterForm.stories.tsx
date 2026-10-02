import type { Meta, StoryObj } from '@storybook/react-vite'
import { expect, userEvent, within } from 'storybook/test'
import { participants } from '../fixtures'
import { RegisterForm } from './RegisterForm'

const meta = {
  title: 'Components/RegisterForm',
  component: RegisterForm,
  args: { workshop: { title: 'Intro to Rust', remainingSeats: 3 }, participants, onSubmit: () => {} },
} satisfies Meta<typeof RegisterForm>
export default meta
type Story = StoryObj<typeof meta>

export const Default: Story = {}
export const WorkshopFull: Story = { args: { workshop: { title: 'Accessible UI', remainingSeats: 0 } } }
export const Submitting: Story = { args: { submitting: true } }
export const LoadingParticipants: Story = { args: { participants: undefined } }
export const ParticipantsFailed: Story = { args: { participants: undefined, participantsError: 'Could not load participants.' } }
export const ValidationError: Story = {
  play: async ({ canvasElement }) => {
    const c = within(canvasElement)
    await userEvent.click(c.getByRole('button', { name: 'Register' }))
    await expect(c.getByText('Choose a participant to register.')).toBeVisible()
  },
}
